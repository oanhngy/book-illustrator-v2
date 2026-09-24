using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using server.Gemini;
namespace server.Tests.Gemini;

public class FakeGeminiClientTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeGeminiClient _client;

    //Fake chỉ nhìn PrevInteractionId, nhưng field=required --> điền giả
    private static readonly JsonElement DummySchema=JsonDocument.Parse("{}").RootElement;

    //before
    public FakeGeminiClientTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"gemini-fixtures-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        WriteFixture("style.json", "{\"style\":\"watercolour\"}");
        WriteFixture("characters.json", "{\"characters\":[]}");
        WriteFixture("chapters.json", "{\"chapters\":[]}");

        //chỉ cần base64 hợp lệ để k throw
        const string fakeImage = "{\"mimeType\":\"image/png\",\"base64\":\"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=\"}";
        WriteFixture("portrait-1.json", fakeImage);
        WriteFixture("portrait-2.json", fakeImage);
        WriteFixture("illustration-1.json", fakeImage);

        _client=new FakeGeminiClient(_tempDir);
    }

    private void WriteFixture(string fileName, string json) => File.WriteAllText(Path.Combine(_tempDir, fileName), json);

    //after
    public void Dispose()
    {
        if(Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive:true);
    }   

    //helper dựng request: Model/Prompt=chuỗi bất kỳ
    private GeminiJsonRequest JsonReq(string? prev) => new ()
    {
        Model="fake-model",
        Prompt="fake-prompt",
        PreviousInteractionId=prev,
        Schema=DummySchema
    };

    private GeminiImageRequest ImageReq(string? prev) => new()
    {
        Model="fake-model",
        Prompt="fake-prompt",
        PreviousInteractionId=prev
    };

    [Fact]
    public async Task FullChain_SixCalls_ChainsCorrectly_NeverThrows()
    {
        //act
        //đúng thứ tự
        var style=await _client.GenerateJsonAsync(JsonReq(null));
        var characters=await _client.GenerateJsonAsync(JsonReq(style.InteractionId));
        var portrait1=await _client.GenerateImageAsync(ImageReq(characters.InteractionId));
        var portrait2=await _client.GenerateImageAsync(ImageReq(portrait1.InteractionId));
        var chapters=await _client.GenerateJsonAsync(JsonReq(characters.InteractionId));
        var illustration1=await _client.GenerateImageAsync(ImageReq(portrait2.InteractionId));

        //assert
        Assert.Equal("fake-json-style", style.InteractionId);
        Assert.Equal("fake-json-characters", characters.InteractionId);
        Assert.Equal("fake-image-portrait-1", portrait1.InteractionId);
        Assert.Equal("fake-image-portrait-2", portrait2.InteractionId);
        Assert.Equal("fake-image-illustration-1", illustration1.InteractionId);

        //xnhan Clone() hd đúng
        Assert.True(characters.Data.TryGetProperty("characters", out _));
        Assert.True(chapters.Data.TryGetProperty("chapters", out _));

        Assert.NotEmpty(portrait1.ImageBytes);
        Assert.Equal("image/png", portrait1.MimeType);
    }

    [Fact]
    public async Task GenerateJsonAsync_UnknowPreviousInteractionId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>( () => _client.GenerateJsonAsync(JsonReq("Not-exist")));
    }

    [Fact]
    public async Task UploadBookAsync_ReturnsNonEmptyUri()
    {
        var uri=await _client.UploadBookAsync("anything in the book");
        Assert.False(string.IsNullOrWhiteSpace(uri));
    }
}