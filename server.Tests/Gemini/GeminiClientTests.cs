using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using server.Gemini;
namespace server.Tests.Gemini;

public class GeminiClientTests
{
    //dựng GeminiClient vs HttpClient fắn stub handler, bắt buộc set BaseAddress vì GeminiClient gọi=link tương đối
    private static GeminiClient MakeClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http=new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress=new Uri("https://generativelanguage.googleapis.com")
        };
        var options=Options.Create(new GeminiOptions { ApiKey="fake-key"});
        return new GeminiClient(http, options);
    }

    private static HttpResponseMessage JsonResponse(string json) => new (HttpStatusCode.OK)
    {
        Content=new StringContent(json, Encoding.UTF8, "application/json")
    };


    //1. happy path-GenerateJsonAsync
    [Fact]
    public async Task GenerateJsonAsync_HappyPath_ParsesIdAndData()
    {
        //arrange
        var client=MakeClient(_ => JsonResponse("""
        {
            "id": "abc-123",
            "status": "completed",
            "steps": [
                {"type": "model_output", "content": [
                    {"type": "text", "text": "{\"characters\":[{\"name\":\"Mole\"}]}"}
                ]}
            ]
        }
        """));

        //act, k phải lượt đầu-->PrevInterationId có giá trị + k BookUri
        var result=await client.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model="gemini-3.8-flash",
            Prompt="describe characters",
            PreviousInteractionId="prev-id",
            Schema=JsonDocument.Parse("{}").RootElement
        });

        //assert
        Assert.Equal("abc-123", result.InteractionId);
        var characters=result.Data.GetProperty("characters");
        Assert.Equal(1, characters.GetArrayLength());
        Assert.Equal("Mole", characters[0].GetProperty("name").GetString());
    }

    //2. GenerateImageAsync
    [Fact]
    public async Task GenerateImageAsync_MultipleImagesInResponse_ReturnsLastOne()
    {
        //arrange: có text xen giữa 2 ảnh, test ##15
        var firstImage=Convert.ToBase64String(Encoding.UTF8.GetBytes("first-image-bytes"));
        var lastImage=Convert.ToBase64String(Encoding.UTF8.GetBytes("last-image-bytes"));
        var client=MakeClient(_ => JsonResponse($$"""
        {
            "id": "img-1",
            "status": "completed",
            "steps": [
                {"type": "model_output", "content": [
                    {"type": "image", "mime_type": "image/png", "data": "{{firstImage}}"},
                    {"type": "text", "text": "caption"},
                    {"type": "image", "mime_type": "image/png", "data": "{{lastImage}}"}
                ]}
            ]
        }
        """));

        //act
        var result=await client.GenerateImageAsync(new GeminiImageRequest
        {
            Model="gemini-3.1-flash-lite-image",
            Prompt="portrait",
            PreviousInteractionId="prev-id"
        });

        //assert
        Assert.Equal("last-image-bytes", Encoding.UTF8.GetString(result.ImageBytes));
    }

    [Fact]
    public async Task PostInteraction_StatusNotCompleted_ThrowsGeminiException()
    {
        //arrange
        var client=MakeClient(_ => JsonResponse("""
        {
            "id": "x",
            "status": "failed",
            "steps": []
        }
        """));

        //act+assert
        await Assert.ThrowsAsync<GeminiException>(() => client.GenerateJsonAsync(new GeminiJsonRequest
        {
            Model="m",
            Prompt="p",
            PreviousInteractionId="prev",
            Schema=JsonDocument.Parse("{}").RootElement
        }));
    }

    [Fact]
    public async Task UploadBookAsync_HappyPath_ReturnsUri()
    {
        //arrange: 2 request tuần tự (start --> upload)
        const string uploadUrl="https://generativelanguage.googleapis.com/resumable/upload/xyz";
        var client=MakeClient(request =>
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("upload/v1beta/files"))
            {
                //b1: url upload ở header
                var response=new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("{}")};
                response.Headers.Add("X-Goog-Upload-URL", uploadUrl);
                return response;
            }
            
            //b2: confirm gửi đúng tới url nhận đc ở b1
            Assert.Equal(uploadUrl, request.RequestUri!.ToString());
            return JsonResponse("""
                {"file": {"uri": "https://generativelanguage.googleapis.com/v1beta/files/abc", "state": "ACTIVE"}}
            """);
        });

        //act
        var uri=await client.UploadBookAsync("book content");

        //assert
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/files/abc", uri); 
    }
}