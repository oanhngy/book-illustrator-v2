using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceRunCharactersTests : IDisposable
{
    private const string BookText="The Mole had been working very hard all the morning.";

    private readonly string _tempDir;
    private readonly ProjectStore _store;

    //BEFORE
    public PipelineServiceRunCharactersTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
    }

    //AFTER
    public void Dispose()
    {
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helper: mỗi test tự chọn Gemini giả (stub hoặc fake) --> truyền vào đây để dựng pipeline
    private PipelineService CreatePipeline(IGeminiClient gemini)
    {
        return new PipelineService(_store, gemini, Options.Create(new GeminiOptions()), NullLogger<PipelineService>.Instance);
    }

    //helper: project đã xong B1 (có style + interaction id), rồi claim B2 thật --> trả job có vé thật
    private async Task<StepJob> SeedClaimedStepTwoAsync(PipelineService pipeline)
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
            CompletedSteps=1,
            Style="watercolor",
            TextInteractionId="interaction-style",
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, BookText);

        var claim=await pipeline.ClaimStepAsync(project.Id, 2, null);
        return new StepJob(project.Id, 2, claim.Ticket!.Value);
    }

    //cap 2 ép ở server + nối đúng thread của B1 + K gửi lại sách (FR-29)
    [Fact]
    public async Task RunStepAsync_GeminiReturnsThreeCharacters_KeepsFirstTwoAndChainsContext()
    {
        //Arrange: Gemini trả 3 nhân vật dù prompt nhờ tối đa 2
        var gemini=new StubGeminiClient();
        gemini.OnJson=_ => Task.FromResult(StubGeminiClient.JsonResult(
            """
            { "characters": [
                { "name": "Mole", "imagePrompt": "a mole" },
                { "name": "Rat", "imagePrompt": "a water rat" },
                { "name": "Toad", "imagePrompt": "a toad" }
            ] }
            """, "interaction-characters"));
        var pipeline=CreatePipeline(gemini);
        var job=await SeedClaimedStepTwoAsync(pipeline);

        //Act
        await pipeline.RunStepAsync(job);

        //Assert 1: chỉ lưu 2 nhân vật đầu, đúng thứ tự; con trỏ ngữ cảnh tiến lên; bước đóng lại
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Equal(["Mole", "Rat"], reloaded.Characters.Select(c => c.Name));
        Assert.Equal("a water rat", reloaded.Characters[1].ImagePrompt);
        Assert.Equal("interaction-characters", reloaded.TextInteractionId);
        Assert.Equal(2, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);

        //Assert 2: đúng 1 lời gọi, nối từ interaction của B1, và sách K nằm trong prompt
        var request=Assert.Single(gemini.JsonRequests);
        Assert.Equal("interaction-style", request.PreviousInteractionId);
        Assert.DoesNotContain(BookText, request.Prompt);
    }

    //B2 lỗi thì kết quả B1 còn nguyên, con trỏ ngữ cảnh k bị đổi --> retry B2 nối đúng chỗ cũ
    [Fact]
    public async Task RunStepAsync_NoValidCharacter_RecordsFailureAndKeepsStepOneResult()
    {
        //Arrange: mảng có 1 item thiếu imagePrompt --> k còn nhân vật hợp lệ nào
        var gemini=new StubGeminiClient();
        gemini.OnJson=_ => Task.FromResult(StubGeminiClient.JsonResult(
            """{ "characters": [ { "name": "Mole" } ] }""", "interaction-broken"));
        var pipeline=CreatePipeline(gemini);
        var job=await SeedClaimedStepTwoAsync(pipeline);

        //Act
        await pipeline.RunStepAsync(job);

        //Assert: B2 thành bước lỗi; B1 vẫn tính là xong, style + interaction id cũ giữ nguyên
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.FailedStep);
        Assert.NotNull(reloaded.LastError);
        Assert.Null(reloaded.RunningStep);
        Assert.Equal(1, reloaded.CompletedSteps);
        Assert.Equal("watercolor", reloaded.Style);
        Assert.Equal("interaction-style", reloaded.TextInteractionId);
        Assert.Empty(reloaded.Characters);
    }

    //B1 --> B2 chạy được với FakeGeminiClient + fixture thật (đường USE_FAKE_GEMINI=true)
    //Fake chọn fixture theo PreviousInteractionId: pipeline lưu/nối id sai là Fake ném ngay
    [Fact]
    public async Task RunStepAsync_StepOneThenTwoWithFakeGemini_ChainsThroughFixtures()
    {
        //Arrange: Fake đọc fixtures thật của server; project mới tinh
        var fixturesDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "fixtures"));
        var pipeline=CreatePipeline(new FakeGeminiClient(fixturesDir));
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, BookText);

        //Act: chạy B1 rồi B2 y như worker sẽ làm (claim --> run)
        var claimOne=await pipeline.ClaimStepAsync(project.Id, 1, null);
        await pipeline.RunStepAsync(new StepJob(project.Id, 1, claimOne.Ticket!.Value));
        var claimTwo=await pipeline.ClaimStepAsync(project.Id, 2, null);
        await pipeline.RunStepAsync(new StepJob(project.Id, 2, claimTwo.Ticket!.Value));

        //Assert: cả 2 bước xong, k lỗi, có style + ít nhất 1 nhân vật và k quá cap
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FailedStep);
        Assert.Equal(2, reloaded.CompletedSteps);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.Style));
        Assert.InRange(reloaded.Characters.Count, 1, 2);
    }
}
