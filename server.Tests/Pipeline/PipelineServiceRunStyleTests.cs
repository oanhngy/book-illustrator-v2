using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceRunStyleTests : IDisposable
{
    private const string BookText="The Mole had been working very hard all the morning.";

    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly StubGeminiClient _gemini;
    private readonly PipelineService _pipeline;

    //chạy BEFORE
    public PipelineServiceRunStyleTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _gemini=new StubGeminiClient();
        _pipeline=new PipelineService(_store, _gemini, Options.Create(new GeminiOptions()), NullLogger<PipelineService>.Instance);
    }

    //chạy AFTER
    public void Dispose()
    {
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helper: tạo project + file sách, rồi claim B1 bằng ClaimStepAsync THẬT --> trả job có vé thật
    private async Task<StepJob> SeedClaimedStepOneAsync(string? requestedStyle=null)
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, BookText);

        var claim=await _pipeline.ClaimStepAsync(project.Id, 1, requestedStyle);
        return new StepJob(project.Id, 1, claim.Ticket!.Value);
    }

    //helper: giả lập "trong lúc Gemini đang chạy, project bị reset rồi claim lại" = đổi vé trên disk
    private Task ReplaceTicketAsync(Guid projectId, DateTime newTicket)
    {
        return _store.UpdateAsync(projectId, p =>
        {
            p.RunningSince=newTicket;
            return true;
        });
    }

    //canh: luồng chính của B1 + lượt gọi đầu gửi sách inline, mở thread mới (##17)
    [Fact]
    public async Task RunStepAsync_NoRequestedStyle_SavesAiStyleAndCompletesStep()
    {
        //Arrange: B1 đã claim, user k nhập style
        var job=await SeedClaimedStepOneAsync();

        //Act
        await _pipeline.RunStepAsync(job);

        //Assert 1: kết quả AI + con trỏ ngữ cảnh được lưu, bước đóng lại, vé được trả
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Equal("stub style", reloaded.Style);
        Assert.Equal("stub-interaction-1", reloaded.TextInteractionId);
        Assert.Equal(1, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);
        Assert.Null(reloaded.RunningSince);
        Assert.Null(reloaded.FailedStep);

        //Assert 2: đúng 1 lời gọi; sách nằm trong prompt, k BookUri, k nối thread cũ
        var request=Assert.Single(_gemini.JsonRequests);
        Assert.Contains(BookText, request.Prompt);
        Assert.Null(request.BookUri);
        Assert.Null(request.PreviousInteractionId);
    }

    //canh: ##18 - user nhập style thì dùng cái user nhập, bỏ text AI
    [Fact]
    public async Task RunStepAsync_RequestedStyle_UsesUserStyleNotAiText()
    {
        //Arrange: user nhập style lúc claim
        var job=await SeedClaimedStepOneAsync("pixel art");

        //Act
        await _pipeline.RunStepAsync(job);

        //Assert: style=cái user nhập (stub trả "stub style" nhưng bị bỏ), vẫn lưu interaction id để B2 nối
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Equal("pixel art", reloaded.Style);
        Assert.Equal("stub-interaction-1", reloaded.TextInteractionId);
        Assert.Equal(1, reloaded.CompletedSteps);
        //prompt gửi đi phải chứa style của user
        Assert.Contains("pixel art", Assert.Single(_gemini.JsonRequests).Prompt);
    }

    //canh: FR-26 lỗi thành dữ liệu trên disk, k kẹt runningStep; FR-28 k auto-retry
    [Fact]
    public async Task RunStepAsync_GeminiThrows_RecordsFailureAndDoesNotThrow()
    {
        //Arrange: Gemini lỗi
        var job=await SeedClaimedStepOneAsync();
        _gemini.OnJson=_ => throw new GeminiException("Gemini returned 503", 503);

        //Act: k được ném ra ngoài (nếu ném, test fail ngay tại dòng này)
        await _pipeline.RunStepAsync(job);

        //Assert: lỗi nằm trên disk, project mở lại để retry, k có kết quả nửa vời
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.FailedStep);
        Assert.Equal("Gemini returned 503", reloaded.LastError);
        Assert.Null(reloaded.RunningStep);
        Assert.Null(reloaded.RunningSince);
        Assert.Equal(0, reloaded.CompletedSteps);
        Assert.Null(reloaded.Style);
        //đúng 1 lời gọi, k tự gọi lại
        Assert.Single(_gemini.JsonRequests);
    }

    //canh: ##19 - lượt đã mất claim k được ghi kết quả (kịch bản 10:00-10:13)
    [Fact]
    public async Task RunStepAsync_ClaimLostWhileGeminiRuns_DiscardsResult()
    {
        //Arrange: stub đổi vé trên disk ngay giữa lời gọi, rồi mới trả kết quả
        var job=await SeedClaimedStepOneAsync();
        var newTicket=job.Ticket.AddMinutes(11);
        _gemini.OnJson=async _ =>
        {
            await ReplaceTicketAsync(job.ProjectId, newTicket);
            return StubGeminiClient.JsonResult("""{ "style": "late style" }""", "late-interaction");
        };

        //Act: lượt cũ trả về sau khi đã mất claim
        await _pipeline.RunStepAsync(job);

        //Assert: kết quả của lượt cũ bị bỏ; state của lượt mới còn nguyên
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.Style);
        Assert.Null(reloaded.TextInteractionId);
        Assert.Equal(0, reloaded.CompletedSteps);
        Assert.Equal(1, reloaded.RunningStep);
        Assert.Equal(newTicket, reloaded.RunningSince);
    }

    //canh: ##19 - nhánh ghi LỖI cũng phải so vé (nhánh dễ bị quên nhất)
    [Fact]
    public async Task RunStepAsync_ClaimLostThenGeminiThrows_DoesNotRecordFailure()
    {
        //Arrange: stub đổi vé trên disk rồi ném lỗi
        var job=await SeedClaimedStepOneAsync();
        var newTicket=job.Ticket.AddMinutes(11);
        _gemini.OnJson=async _ =>
        {
            await ReplaceTicketAsync(job.ProjectId, newTicket);
            throw new GeminiException("Gemini timeout");
        };

        //Act
        await _pipeline.RunStepAsync(job);

        //Assert: lượt cũ lỗi k đè failedStep lên lượt mới đang chạy
        var reloaded=await _store.GetAsync(job.ProjectId);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.FailedStep);
        Assert.Null(reloaded.LastError);
        Assert.Equal(1, reloaded.RunningStep);
        Assert.Equal(newTicket, reloaded.RunningSince);
    }
}
