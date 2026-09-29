using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
namespace server.Gemini;

public class GeminiClient : IGeminiClient
{
    private const string ImageMimeType="image/png";
    private const string ImageAspectRatio="3:4";
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;

    //HttpClient do IHttpClientFactory cấp (typed client, task 4 đky)
    public GeminiClient(HttpClient http, IOptions<GeminiOptions> options)
    {
        _http=http;
        _options=options.Value;
    }

    public async Task<string> UploadBookAsync(string bookText)
    {
        var bytes=Encoding.UTF8.GetBytes(bookText);
        //b1- mở phiên upload
        using var startRequest=new HttpRequestMessage(HttpMethod.Post, "upload/v1beta/files");
        startRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
        startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "start");
        startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Length", bytes.Length.ToString());
        startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Type", "text/plain");
        startRequest.Content=new StringContent("{\"file\":{\"display_name\":\"book.txt\"}}", Encoding.UTF8, "application/json");

        using var startResponse=await SendAsync(startRequest);
        await EnsureSuccessAsync(startResponse);

        if(!startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var urls))
            throw new GeminiException("Gemini do not response header X-Goog-Upload-URL after starting upload");
        var uploadUrl=urls.First();

        //b2-gửi nội dung
        using var uploadRequest=new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Content=new ByteArrayContent(bytes); //raw bytes k bọc json wrapper

        using var uploadResponse=await SendAsync(uploadRequest);
        var root=await ReadJsonAsync(uploadResponse);
        
        //lấy file.uri gán Project.BookUri, check file ready chưa
        if(!root.TryGetProperty("file", out var file))  
            throw new GeminiException("Response upload lack of object 'file'");
        if(file.TryGetProperty("state", out var state) && state.GetString() != "ACTIVE")
            throw new GeminiException($"Book file does not ready, state={state.GetString()}");

        return GetRequiredString(file, "uri");
    }

    public async Task<GeminiJsonResult> GenerateJsonAsync(GeminiJsonRequest request)
    {
        // LƯỢT ĐẦU k có book thì báo lỗi ngay, k gửi request
        if(request.PreviousInteractionId==null && request.BookUri==null)
            throw new ArgumentException("First JSON (PreviousInteractionId==null must have BookUri");
    
        //input: sách(lượt đầu only, BookUri null các lượt sau)+prompt
        var input=new JsonArray();
        if(request.BookUri != null)
        {
            input.Add(new JsonObject
            {
                ["type"]="document",
                ["uri"]=request.BookUri,
                ["mime_type"]="text/plain"
            });
        }
        
        input.Add(new JsonObject
        {
            ["type"]="text",
            ["text"]=request.Prompt
        });
        
        var body=BuildBody(request.Model, request.PreviousInteractionId, input);

        //ép Gemini trả json đúng schema
        body["response_format"]=new JsonObject
        {
            ["type"]="text",
            ["mime_type"]="application/json",
            ["schema"]=JsonNode.Parse(request.Schema.GetRawText())
        };

        var root=await PostInteractionAsync(body);

        //text json ở item type "text" trong steps, lấy item text cuối
        string? text = null;
        foreach (var item in ContentItems(root))
        {
            if (TypeOf(item) == "text") text = item.GetProperty("text").GetString();
        }
        if(string.IsNullOrEmpty(text))
            throw new GeminiException("Gemini Response do not have content text");

        //in case parse lỗi
        JsonElement data;
        try
        {
            using var doc=JsonDocument.Parse(text);
            data=doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new GeminiException("Gemini response text, not valid JSON", inner:ex);
        }
        return new GeminiJsonResult
        {
            InteractionId=GetRequiredString(root, "id"),
            Data=data
        };
    }

    public async Task<GeminiImageResult> GenerateImageAsync(GeminiImageRequest request)
    {
        var input=new JsonArray { new JsonObject
        {
           ["type"]="text",
            ["text"]=request.Prompt 
        }};

        var body=BuildBody(request.Model, request.PreviousInteractionId, input);

        //ycau output la hình
        body["response_format"]= new JsonObject
        {
            ["type"]="image",
            ["mime_type"]=ImageMimeType,
            ["aspect_ratio"]=ImageAspectRatio
        };
        var root=await PostInteractionAsync(body);

        //##15: response may contains text + plural pictures, only keep last pic, "keep last pic" thuộc tầng Gemini, Pipeline k biết
        JsonElement? lastImage=null;
        foreach(var item in ContentItems(root))
        {
            if(TypeOf(item)=="image") lastImage=item;
        }
        if(lastImage is null)
            throw new GeminiException("Gemini Response does not contain any image");

        byte[] imageBytes;
        try
        {
            imageBytes=Convert.FromBase64String(GetRequiredString(lastImage.Value, "data"));
        }
        catch (FormatException ex)
        {
            throw new GeminiException("Image data Gemini response is not valid base64", inner:ex);
        }
        return new GeminiImageResult
        {
            InteractionId=GetRequiredString(root, "id"),
            ImageBytes=imageBytes,
            MimeType=GetRequiredString(lastImage.Value, "mime_type")
        };
    }

    //helpers
    //body giống nha cho mọi lượt gọi interactions
    private JsonObject BuildBody(string model, string? previousInteractionId, JsonArray input)
    {
        var body=new JsonObject
        {
            ["model"]=model,
            ["system_instruction"]=_options.SystemInstruction, //gửi lại mọi lượt ##14
            ["input"]=input
        };
        //cho lượt đầu only
        if(previousInteractionId != null) body["previous_interaction_id"]=previousInteractionId;
        return body;
    }

    //HTTP POST Gem Interaction, bọc logic auth, đọc JSON trả lại, check tính toàn vẹn status=completed
    private async Task<JsonElement> PostInteractionAsync(JsonObject body)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post, "v1beta/interactions");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content=new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response=await SendAsync(request);
        var root=await ReadJsonAsync(response);

        var status=root.TryGetProperty("status", out var s) ? s.GetString() : null;
        if(status != "completed") throw new GeminiException($"Interaction do not completed, status={status ?? "(missing)"}");
        return root;
    }

    //flatten cây ctruc phân tầng trong Interactions API thành chuỗi tuần tự, only lấy trong "model_output"
    //yield=on-demand sinh p.tử, k cần tạo List-->tiết kiệm RAM+faster; đi kèm yiel return... và yield break;là lazy evaluation(trả theo ycau/demand)
    private static IEnumerable<JsonElement> ContentItems(JsonElement root)
    {
        if(!root.TryGetProperty("steps", out var steps)) yield break;
        foreach(var step in steps.EnumerateArray())
        {
            if(TypeOf(step) != "model_output") continue;
            if(!step.TryGetProperty("content", out var content)) continue;
            foreach(var item in content.EnumerateArray()) yield return item;
        }
    }

    //nhận diện JsonElement là type text/image
    private static string? TypeOf(JsonElement element) => 
        element.TryGetProperty("type", out var t) ? t.GetString() : null;

    //lấy field string bắt buộc, thiếu-->throw
    private static string GetRequiredString(JsonElement element, string name)
    {
        if(element.TryGetProperty(name, out var value) && value.GetString() is { } str) return str;
        throw new GeminiException($"Gemini Response  missing field '{name}");
    }

    //gom lỗi mạng, timeout thàng 1 loại exception, phase 6 only need to catch GeminiException
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        try
        {
            return await _http.SendAsync(request);
        }
        catch(HttpRequestException ex)
        {
            throw new GeminiException("Can not connect to Gemini", inner:ex);
        }
        catch(TaskCanceledException ex)
        {
            throw new GeminiException("Gemini timeout", inner:ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode) return;
        var body=await response.Content.ReadAsStringAsync();
        throw new GeminiException($"Gemini response HTTP {(int)response.StatusCode} : {body}", (int)response.StatusCode);
    }

    //check 2xx r parse body thành JsonElement độc lập(clone) để trả về an toàn
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        var text=await response.Content.ReadAsStringAsync();
        try
        {
            using var doc=JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch(Exception ex)
        {
            throw new GeminiException("Response Gemini is not valid JSON", inner:ex);
        }
    }
}