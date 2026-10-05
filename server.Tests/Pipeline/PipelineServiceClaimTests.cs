using Microsoft.AspNetCore.Authentication;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineServiceClaimTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly PipelineService _pipeline;

    //before
    public PipelineServiceClaimTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-tests-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _pipeline=new PipelineService(_store);
    }

    //after
    public void Dispose()
    {
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helpper: tạo 1 proj ở disk, configure để each test chỉnh state khởi điểm
    private async Task<Project> SeedAsync(Action<Project>? configure=null)
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Proj",
            CreatedAt=DateTime.UtcNow,
        };
        configure?.Invoke(project);
        await _store.SaveAsync(project);
        return project;
    }

    [Fact]
    public async Task ClaimStepAsync_FreshProject_ClaimsAndPersistsTicket()
    {
        //arrange
        var project=await SeedAsync();

        //act
        var result=await _pipeline.ClaimStepAsync(project.Id, 1, "watercolor");

        //assert
        //đọc lại disk
        var reloaded=await _store.GetAsync(project.Id);
        Assert.Equal(ClaimStatus.Claimed, result.Status);
        Assert.NotNull(result.Ticket);
        Assert.Equal(1, reloaded.RunningStep);
        //vé trả phải = đúng gtri trên disk
        Assert.Equal(result.Ticket, reloaded.RunningSince);
        Assert.Equal("watercolor", reloaded.RequestedStyle);
    }

    [Fact]
    public async Task ClaimStepAsync_StepAlreadyRunning_RejectedAndDiskUnchanged()
    {
        //arrange: b1 chạy từ trước, có vé
        var since=new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var project=await SeedAsync( p =>
        {
            p.RunningStep=1;
            p.RunningSince=since;
        });

        //act: request 1nd xin chạy lại đúng b1
        var result=await _pipeline.ClaimStepAsync(project.Id, 1, null);

        //assert
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(new ClaimResult(ClaimStatus.AlreadyRunning, null), result);
        Assert.Equal(since, reloaded.RunningSince);
    }

    [Fact]
    public async Task ClaimStepAsync_PreviousStepNotDone_WrongOrder()
    {
        //arrange
        var project=await SeedAsync();

        //act: xin chạy b2 nhưng b1 chưa xong
        var result=await _pipeline.ClaimStepAsync(project.Id, 2, null);

        //assert
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(new ClaimResult(ClaimStatus.WrongOrder, null), result);
        Assert.Null(reloaded.RunningStep);
    }

    [Fact]
    public async Task ClaimStepAsync_StepAlreadyCompleted_WrongOrder()
    {
         //arrange
         var project=await SeedAsync(p => p.CompletedSteps=1);

         //act
         var result=await _pipeline.ClaimStepAsync(project.Id, 1, null);

         //aseert
         Assert.Equal(new ClaimResult(ClaimStatus.WrongOrder, null), result);
    }

    [Fact]
    public async Task ClaimStepAsync_ProjectMissing_NotFound()
    {
        //arrange
        var missingId=Guid.NewGuid();

        //act
        var result=await _pipeline.ClaimStepAsync(missingId, 1, null);

        //assert
        Assert.Equal(new ClaimResult(ClaimStatus.NotFound, null), result);
    }

    [Fact]
    public async Task ClaimStepAsync_TwentyParallelClaims_ExactlyOneWins()
    {
        //arrange
        var project=await SeedAsync();

        //act
        var tasks=Enumerable.Range(0,20).Select(_ =>
            _pipeline.ClaimStepAsync(project.Id, 1, null));
        var results=await Task.WhenAll(tasks);

        //assert
        Assert.Equal(1, results.Count(r => r.Status==ClaimStatus.Claimed));
        Assert.Equal(19, results.Count(r => r.Status==ClaimStatus.AlreadyRunning));
    }

    [Fact]
    public async Task ClaimStepAsync_RejectedClaim_DoesNotOverwriteRequestedStyle()
    {
        //arrange: đã claim b1 vs style watercolor
        var project=await SeedAsync();
        await _pipeline.ClaimStepAsync(project.Id, 1, "watercolor");

        //act: xin chạy b1 vs style #
        var loser=await _pipeline.ClaimStepAsync(project.Id, 1, "pixel art");

        //assert
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(ClaimStatus.AlreadyRunning, loser.Status);
        Assert.Equal("watercolor", reloaded.RequestedStyle);
    }

    [Fact]
    public async Task ClaimStepAsync_StepTwo_KeepsRequestedStyle()
    {
        //arrange
        var project=await SeedAsync( p =>
        {
            p.CompletedSteps=1;
            p.RequestedStyle="watercolor";
        });

        //act:claim b2, truyền null
        var result=await _pipeline.ClaimStepAsync(project.Id, 2, null);

        //assert
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(ClaimStatus.Claimed, result.Status);
        Assert.Equal("watercolor", reloaded.RequestedStyle);
    }

    [Fact]
    public async Task ClaimStepAsync_RetryFailedStep_ClearsAndStoreNewStyle()
    {
        //arrange: b1 lỗi, còn lỗi cũ + style cũ ở disk
        var project=await SeedAsync( p =>
        {
            p.FailedStep=1;
            p.RequestedStyle="watercolor";
            p.LastError="Gemini timeout";
        });

        //act: retry b1, k gửi style
        var result=await _pipeline.ClaimStepAsync(project.Id, 1, null);

        //assert: lỗi cũ xóa, style=null
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(ClaimStatus.Claimed, result.Status);
        Assert.Null(reloaded.FailedStep);
        Assert.Null(reloaded.LastError);
        Assert.Null(reloaded.RequestedStyle);
    }
}