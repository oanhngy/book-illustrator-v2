using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceRunChaptersTests : IDisposable
{
    private const string BookText="The Mole had been working very hard all the morning.";

    private readonly string _tempDir;
    private readonly ProjectStore _store;

    //BEFORE
    public PipelineServiceRunChaptersTests()
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

    //helper: mỗi test tự chọn Gemini giả (stub hoặc fake)
    private PipelineService CreatePipeline(IGeminiClient gemini)
    {
        return new PipelineService(_store, gemini, Options.Create(new GeminiOptions()), NullLogger<PipelineService>.Instance);
    }

    //helper: project đã xong B1-B3 --> có CẢ 2 con trỏ (text dừng ở Characters, ảnh dừng ở portrait cuối)
    //rồi claim B4 thật --> trả job có vé thật
    private async Task<StepJob> SeedClaimedStepFourAsync(PipelineService pipeline, string textInteractionId, string imageInteractionId)
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
            CompletedSteps=3,
            Style="watercolor",
            Characters=[ new Character { Name="Mole", ImagePrompt="a mole" } ],
            TextInteractionId=textInteractionId,
            ImageInteractionId=imageInteractionId,
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, BookText);

        var claim=await pipeline.ClaimStepAsync(project.Id, 4, null);
        return new StepJob(project.Id, 4, claim.Ticket!.Value);
    }

    //B4 nối từ luồng TEXT dù lượt gần nhất là ảnh, và k làm xê dịch con trỏ luồng ảnh
    //+ cap 1 chương ép ở server (FR-30) + k gửi lại sách (FR-29)
    [Fact]
    public async Task RunStepAsync_StepFour_ChainsFromTextThreadAndKeepsFirstChapter()
    {
        //Arrange: Gemini trả 2 chương dù prompt nhờ chỉ 1
        var gemini=new StubGeminiClient();
        gemini.OnJson=_ => Task.FromResult(StubGeminiClient.JsonResult(
            """
            { "chapters": [
                { "title": "The River Bank", "summary": "Mole meets Rat", "imagePrompt": "a mole by a river" },
                { "title": "The Open Road", "summary": "They visit Toad", "imagePrompt": "a toad in a car" }
            ] }
            """, "interaction-chapters"));
        var pipeline=CreatePipeline(gemini);
        var job=await SeedClaimedStepFourAsync(pipeline, "interaction-characters", "interaction-portrait-2");

        //Act
        await pipeline.RunStepAsync(job);

        //Assert 1: request nối từ con trỏ TEXT (k phải con trỏ ảnh), sách k nằm trong prompt
        var request=Assert.Single(gemini.JsonRequests);
        Assert.Equal("interaction-characters", request.PreviousInteractionId);
        Assert.DoesNotContain(BookText, request.Prompt);

        //Assert 2: chỉ lưu chương đầu; con trỏ text tiến lên, con trỏ ảnh GIỮ NGUYÊN; bước đóng lại
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        var chapter=Assert.Single(reloaded.Chapters);
        Assert.Equal("The River Bank", chapter.Title);
        Assert.Equal("a mole by a river", chapter.ImagePrompt);
        Assert.Equal("interaction-chapters", reloaded.TextInteractionId);
        Assert.Equal("interaction-portrait-2", reloaded.ImageInteractionId);
        Assert.Equal(4, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);
    }

    //B4 chạy được với FakeGeminiClient + fixture thật (fixture có 4 chương --> cap phải cắt còn 1)
    //Fake ném lỗi nếu B4 nối từ id của luồng ảnh --> bắt luôn cạm bẫy ##22 trên đường USE_FAKE_GEMINI=true
    [Fact]
    public async Task RunStepAsync_StepFourWithFakeGemini_KeepsOneChapterFromFixture()
    {
        //Arrange: 2 con trỏ mang đúng id mà Fake phát ra sau B2 và sau portrait 2
        var fixturesDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "fixtures"));
        var pipeline=CreatePipeline(new FakeGeminiClient(fixturesDir));
        var job=await SeedClaimedStepFourAsync(pipeline, "fake-json-characters", "fake-image-portrait-2");

        //Act
        await pipeline.RunStepAsync(job);

        //Assert: k lỗi, đúng 1 chương dù fixture có 4
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FailedStep);
        Assert.Equal(4, reloaded.CompletedSteps);
        Assert.Single(reloaded.Chapters);
    }
}
