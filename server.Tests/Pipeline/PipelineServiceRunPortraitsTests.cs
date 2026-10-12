using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceRunPortraitsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly ImageStore _images;

    //before
    public PipelineServiceRunPortraitsTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _images=new ImageStore(Path.Combine(_tempDir, "images"));
    }

    //after
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
        return new PipelineService(_store, _images, gemini, Options.Create(new GeminiOptions()), NullLogger<PipelineService>.Instance);
    }

    //helper: project đã xong B1-B2, có 2 nhân vật, con trỏ text dừng ở lượt Characters, CHƯA có ảnh nào
    private async Task<Project> SeedAfterStepTwoAsync()
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
            CompletedSteps=2,
            Style="watercolor",
            Characters=
            [
                new Character { Name="Mole", ImagePrompt="a mole" },
                new Character { Name="Rat", ImagePrompt="a water rat" },
            ],
            TextInteractionId="interaction-characters",
        };
        await _store.SaveAsync(project);
        return project;
    }

    //helper: claim B3 thật rồi chạy; dùng lại được cho lần retry (claim lại bước đang failedStep)
    private static async Task ClaimAndRunStepThreeAsync(PipelineService pipeline, Guid projectId)
    {
        var claim=await pipeline.ClaimStepAsync(projectId, 3, null);
        await pipeline.RunStepAsync(new StepJob(projectId, 3, claim.Ticket!.Value));
    }

    //uồng chính của B3 - chuỗi ngữ cảnh ảnh (##22), lưu file + metadata, và save-as-go(FR32)
    [Fact]
    public async Task RunStepAsync_TwoCharacters_SavesTwoPortraitsAndChainsImages()
    {
        //Arrange: lúc Gemini nhận lời gọi thứ 2, ghi lại số ảnh ĐANG có trên disk
        var gemini=new StubGeminiClient();
        var project=await SeedAfterStepTwoAsync();
        var imagesOnDiskAtSecondCall=-1;
        gemini.OnImage=async _ =>
        {
            if(gemini.ImageRequests.Count==2)
            {
                imagesOnDiskAtSecondCall=(await _store.GetAsync(project.Id))!.Images.Count;
            }
            return StubGeminiClient.ImageResult($"stub-image-{gemini.ImageRequests.Count}");
        };
        var pipeline=CreatePipeline(gemini);

        //Act
        await ClaimAndRunStepThreeAsync(pipeline, project.Id);

        //Assert 1: ảnh đầu nối từ luồng text (điểm giao), ảnh sau nối từ ảnh trước
        Assert.Equal(2, gemini.ImageRequests.Count);
        Assert.Equal("interaction-characters", gemini.ImageRequests[0].PreviousInteractionId);
        Assert.Equal("stub-image-1", gemini.ImageRequests[1].PreviousInteractionId);

        //Assert 2: ảnh thứ nhất đã nằm trên disk TRƯỚC khi ảnh thứ hai được sinh (save-as-you-go)
        Assert.Equal(1, imagesOnDiskAtSecondCall);

        //Assert 3: 2 metadata đúng step + index, file thật tồn tại; con trỏ ảnh ở ảnh cuối, con trỏ text giữ nguyên; bước đóng
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal([0, 1], reloaded.Images.Select(i => i.Index));
        Assert.All(reloaded.Images, i => Assert.Equal(3, i.Step));
        Assert.All(reloaded.Images, i => Assert.True(File.Exists(_images.FullPathOf(i.Path))));
        Assert.Equal("stub-image-2", reloaded.ImageInteractionId);
        Assert.Equal("interaction-characters", reloaded.TextInteractionId);
        Assert.Equal(3, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);
    }

    //##23 - ảnh lỗi --> skip, ảnh sau vẫn vẽ; retry chỉ vẽ ảnh thiếu và nối từ ảnh thành công gần nhất
    [Fact]
    public async Task RunStepAsync_FirstPortraitFails_KeepsSecondThenRetryOnlyGeneratesMissing()
    {
        //Arrange: lời gọi ảnh đầu tiên lỗi, các lời gọi sau bình thường
        var gemini=new StubGeminiClient();
        gemini.OnImage=_ =>
        {
            if(gemini.ImageRequests.Count==1) throw new GeminiException("Gemini returned 503", 503);
            return Task.FromResult(StubGeminiClient.ImageResult($"stub-image-{gemini.ImageRequests.Count}"));
        };
        var pipeline=CreatePipeline(gemini);
        var project=await SeedAfterStepTwoAsync();

        //Act 1: lượt chạy đầu
        await ClaimAndRunStepThreeAsync(pipeline, project.Id);

        //Assert 1: Rat (index 1) vẫn được vẽ và lưu; bước thành lỗi, nêu tên nhân vật thiếu; B2 vẫn tính là xong
        var afterFirstRun=await _store.GetAsync(project.Id);
        Assert.NotNull(afterFirstRun);
        Assert.Equal(2, gemini.ImageRequests.Count);
        Assert.Equal([1], afterFirstRun.Images.Select(i => i.Index));
        Assert.Equal(3, afterFirstRun.FailedStep);
        Assert.Contains("Mole", afterFirstRun.LastError);
        Assert.Equal(2, afterFirstRun.CompletedSteps);

        //Act 2: user bấm retry B3
        await ClaimAndRunStepThreeAsync(pipeline, project.Id);

        //Assert 2: chỉ THÊM 1 lời gọi (cho Mole), nối từ ảnh của Rat; giờ đủ 2 ảnh, bước xong, hết lỗi
        var afterRetry=await _store.GetAsync(project.Id);
        Assert.NotNull(afterRetry);
        Assert.Equal(3, gemini.ImageRequests.Count);
        Assert.Equal("stub-image-2", gemini.ImageRequests[2].PreviousInteractionId);
        Assert.Equal([0, 1], afterRetry.Images.Select(i => i.Index).Order());
        Assert.Equal(3, afterRetry.CompletedSteps);
        Assert.Null(afterRetry.FailedStep);
    }

    //canh: ##19 + ##23 - mất vé giữa bước thì dừng vòng lặp NGAY, k sinh thêm ảnh (mỗi ảnh là tiền)
    [Fact]
    public async Task RunStepAsync_ClaimLostAfterFirstPortrait_StopsWithoutGeneratingSecond()
    {
        //Arrange: trong lúc ảnh đầu đang được sinh, vé trên disk bị đổi (giả lập reset + claim lại)
        var gemini=new StubGeminiClient();
        var project=await SeedAfterStepTwoAsync();
        var newTicket=DateTime.UtcNow.AddMinutes(11);
        gemini.OnImage=async _ =>
        {
            await _store.UpdateAsync(project.Id, p =>
            {
                p.RunningSince=newTicket;
                return true;
            });
            return StubGeminiClient.ImageResult("late-image");
        };
        var pipeline=CreatePipeline(gemini);

        //Act
        await ClaimAndRunStepThreeAsync(pipeline, project.Id);

        //Assert: đúng 1 lời gọi (ảnh thứ 2 k được sinh); k metadata nào được ghi; state của lượt mới còn nguyên
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Single(gemini.ImageRequests);
        Assert.Empty(reloaded.Images);
        Assert.Null(reloaded.ImageInteractionId);
        Assert.Null(reloaded.FailedStep);
        Assert.Equal(newTicket, reloaded.RunningSince);
    }

    //canh: B1 --> B4 chạy liền mạch với FakeGeminiClient + fixture thật (đường USE_FAKE_GEMINI=true)
    //Fake chọn fixture ảnh theo PreviousInteractionId --> nối sai id là ra sai ảnh hoặc ném lỗi
    [Fact]
    public async Task RunStepAsync_StepsOneToFourWithFakeGemini_ProducesTwoPortraitFiles()
    {
        //Arrange: project mới tinh + sách
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
        await _store.SaveBookTextAsync(project.Id, "The Mole had been working very hard all the morning.");

        //Act: chạy lần lượt B1 --> B4 (claim --> run), y như user bấm 4 lần
        for(var step=1; step<=4; step++)
        {
            var claim=await pipeline.ClaimStepAsync(project.Id, step, null);
            await pipeline.RunStepAsync(new StepJob(project.Id, step, claim.Ticket!.Value));
        }

        //Assert: 4 bước xong, k lỗi; 2 portrait có file thật; con trỏ ảnh dừng ở portrait 2, con trỏ text ở lượt Chapters
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FailedStep);
        Assert.Equal(4, reloaded.CompletedSteps);
        Assert.Equal(2, reloaded.Images.Count(i => i.Step==3));
        Assert.All(reloaded.Images, i => Assert.True(new FileInfo(_images.FullPathOf(i.Path)).Length>1000));
        Assert.Equal("fake-image-portrait-2", reloaded.ImageInteractionId);
        Assert.Single(reloaded.Chapters);
    }
}