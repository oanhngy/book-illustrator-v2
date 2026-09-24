using System.Text.Json;
namespace server.Gemini;

//Fixture=PreviousInteractionId của chính request(stateless)
public class FakeGeminiClient : IGeminiClient
{
    private readonly string _fixturesDir;
    public FakeGeminiClient(string fixturesDir="fixtures")
    {
        _fixturesDir=fixturesDir;
    }

    //k cần fixture, trả uri cố định để Project.BokUri lấy
    public Task<string> UploadBookAsync(string bookText)
    {
        return Task.FromResult("fake://book-uri");
    }

    public async Task<GeminiJsonResult> GenerateJsonAsync(GeminiJsonRequest request)
    {
        var(fixtureFile, nextInteractionId)=request.PreviousInteractionId switch
        {
            null => ("style.json", "fake-json-style"), //1. tạo style
            "fake-json-style" => ("characters.json", "fake-json-characters"), //2. tạo character
            "fake-json-characters" => ("chapters.json", "fake-json-schema"), //5. tạo chapters
            _ => throw new InvalidOperationException($"FakeGeminiClient: Do not recognize PreviousInteractionId='{request.PreviousInteractionId}' for GenerateJsonAsync")
        };

        var json=await File.ReadAllTextAsync(Path.Combine(_fixturesDir, fixtureFile));
        using var doc=JsonDocument.Parse(json);
        return new GeminiJsonResult
        {
            InteractionId=nextInteractionId,
            Data=doc.RootElement.Clone() //Clone()= doc dispose khi hết scope
        };
    }

    public async Task<GeminiImageResult> GenerateImageAsync(GeminiImageRequest request)
    { 
        var(fixtureFile, nextInteractionId)=request.PreviousInteractionId switch
        {
            "fake-json-characters" => ("portrait-1.json", "fake-image-portrait-1"), //3. điểm giao text và image, tạo portrait 1
            "fake-image-portrait-1" => ("portrait-2.json", "fake-image-portrait-2"), //4. tạo portrait 2
            "fake-image-portrait-2" => ("illustration-1.json", "fake-image-illustration-1"), //6. tạo illustration
            _ => throw new InvalidOperationException ($"FakeGeminiClient: Do not recognize PreviousInteractionId='{request.PreviousInteractionId}' for GenerateImageAsync")
        };

        var json=await File.ReadAllTextAsync(Path.Combine(_fixturesDir, fixtureFile));
        using var doc=JsonDocument.Parse(json);
        var root=doc.RootElement;

        return new GeminiImageResult
        {
            InteractionId = nextInteractionId,
            ImageBytes = Convert.FromBase64String(root.GetProperty("base64").GetString()!),
            MimeType = root.GetProperty("mimeType").GetString()!
        };
    }
}