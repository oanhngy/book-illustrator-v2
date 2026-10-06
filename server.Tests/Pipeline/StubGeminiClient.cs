using System.Text.Json;
using server.Gemini;
namespace server.Tests.Pipeline;

//test double viết tay cho IGeminiClient: ghi lại mọi request + cho test tự quyết response
//k dùng FakeGeminiClient vì test cần 2 thứ nó k làm được: ném lỗi theo ý, và chen hành động vào "lúc Gemini đang chạy"
public class StubGeminiClient : IGeminiClient
{
    //mọi request JSON đã nhận --> test đếm số lời gọi + soi nội dung prompt
    public List<GeminiJsonRequest> JsonRequests {get;}=[];

    //test gán hàm này để quyết định trả gì/ném gì; mặc định trả 1 style hợp lệ
    public Func<GeminiJsonRequest, Task<GeminiJsonResult>> OnJson {get; set;}
        = _ => Task.FromResult(JsonResult("""{ "style": "stub style" }""", "stub-interaction-1"));

    //helper dựng GeminiJsonResult từ chuỗi JSON
    public static GeminiJsonResult JsonResult(string json, string interactionId)
    {
        using var doc=JsonDocument.Parse(json);
        return new GeminiJsonResult
        {
            InteractionId=interactionId,
            Data=doc.RootElement.Clone()
        };
    }

    public Task<GeminiJsonResult> GenerateJsonAsync(GeminiJsonRequest request)
    {
        JsonRequests.Add(request);
        return OnJson(request);
    }

    public Task<string> UploadBookAsync(string bookText) => throw new NotSupportedException();
    public Task<GeminiImageResult> GenerateImageAsync(GeminiImageRequest request) => throw new NotSupportedException();
}
