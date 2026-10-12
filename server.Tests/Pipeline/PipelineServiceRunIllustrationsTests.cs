// server.Tests/Pipeline/PipelineServiceRunIllustrationsTests.cs
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceRunIllustrationsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly ImageStore _images;

    //chạy BEFORE
    public PipelineServiceRunIllustrationsTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _images=new ImageStore(Path.Combine(_tempDir, "images"));
    }

    //chạy AFTER
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

    //##22 - B5 nối từ luồng ẢNH (portrait cuối), k phải luồng text; mô tả chương nằm trong prompt
    //+ resume lọc theo STEP: portrait index 0 của B3 k được làm B5 bỏ qua chương index 0
    [Fact]
    public async Task RunStepAsync_StepFive_ChainsFromLastPortraitAndAddsIllustration()
    {
        //Arrange: project đã xong B1-B4: 2 portrait (step 3, index 0 và 1), 1 chương, 2 con trỏ khác nhau
        var gemini=new StubGeminiClient();
        var pipeline=CreatePipeline(gemini);
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
            CompletedSteps=4,
            Style="watercolor",
            Chapters=[ new Chapter { Title="The River Bank", Summary="Mole meets Rat", ImagePrompt="a mole and a rat in a boat" } ],
            Images=
            [
                new ImageRef { Id="portrait-0", Step=3, Index=0, Path="x/portrait-0.jpg", MimeType="image/jpeg" },
                new ImageRef { Id="portrait-1", Step=3, Index=1, Path="x/portrait-1.jpg", MimeType="image/jpeg" },
            ],
            TextInteractionId="interaction-chapters",
            ImageInteractionId="interaction-portrait-2",
        };
        await _store.SaveAsync(project);

        //Act
        var claim=await pipeline.ClaimStepAsync(project.Id, 5, null);
        await pipeline.RunStepAsync(new StepJob(project.Id, 5, claim.Ticket!.Value));

        //Assert 1: đúng 1 lời gọi ảnh, nối từ portrait cuối (k phải lượt Chapters), prompt mang mô tả chương
        var request=Assert.Single(gemini.ImageRequests);
        Assert.Equal("interaction-portrait-2", request.PreviousInteractionId);
        Assert.Contains("a mole and a rat in a boat", request.Prompt);

        //Assert 2: thêm đúng 1 ảnh step 5 index 0; 2 portrait cũ còn nguyên; con trỏ ảnh tiến, con trỏ text giữ nguyên; pipeline xong
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded.Images.Count);
        var illustration=Assert.Single(reloaded.Images, i => i.Step==5);
        Assert.Equal(0, illustration.Index);
        Assert.True(File.Exists(_images.FullPathOf(illustration.Path)));
        Assert.Equal("stub-image-1", reloaded.ImageInteractionId);
        Assert.Equal("interaction-chapters", reloaded.TextInteractionId);
        Assert.Equal(5, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);
    }

    //DoD Phase 7 - chạy hết 5 bước với FakeGeminiClient, data/ có đúng số file mong đợi
    //+ ảnh minh họa phải đúng là fixture illustration-1 (nối sai id thì Fake trả nhầm portrait)
    [Fact]
    public async Task RunStepAsync_AllFiveStepsWithFakeGemini_ProducesExpectedFiles()
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

        //Act: user bấm lần lượt 5 bước (claim --> run)
        for(var step=1; step<=5; step++)
        {
            var claim=await pipeline.ClaimStepAsync(project.Id, step, null);
            await pipeline.RunStepAsync(new StepJob(project.Id, step, claim.Ticket!.Value));
        }

        //Assert 1: pipeline xong, k lỗi; 2 portrait + 1 illustration trong metadata
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FailedStep);
        Assert.Equal(5, reloaded.CompletedSteps);
        Assert.Equal(2, reloaded.Images.Count(i => i.Step==3));
        var illustration=Assert.Single(reloaded.Images, i => i.Step==5);

        //Assert 2: trên disk có đúng 3 file ảnh cho project này, k thừa k thiếu, k còn file .tmp
        var imageFiles=Directory.GetFiles(Path.Combine(_tempDir, "images", project.Id.ToString()));
        Assert.Equal(3, imageFiles.Length);
        Assert.All(imageFiles, f => Assert.EndsWith(".jpg", f));

        //Assert 3: file illustration có nội dung đúng bằng fixture illustration-1.json (k phải 1 trong 2 portrait)
        using var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixturesDir, "illustration-1.json")));
        var expectedBytes=Convert.FromBase64String(fixture.RootElement.GetProperty("base64").GetString()!);
        var actualBytes=await File.ReadAllBytesAsync(_images.FullPathOf(illustration.Path));
        Assert.Equal(expectedBytes, actualBytes);
    }
}
