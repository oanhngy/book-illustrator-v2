using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using server.Endpoints;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
using server.Tests.Pipeline;
namespace server.Tests.Endpoints;

//test gọi THẲNG handler PipelineEndpoints.RunStepAsync (k qua HTTP thật)
//integration test qua HTTP bằng WebApplicationFactory để dành Phase 10
public class PipelineEndpointsTests : IDisposable
{
    private const string Owner="owner@example.com";

    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly StubGeminiClient _gemini;
    private readonly Channel<StepJob> _channel;
    private readonly ServiceProvider _provider;

    //chạy BEFORE
    public PipelineEndpointsTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _gemini=new StubGeminiClient();
        _channel=Channel.CreateUnbounded<StepJob>();

        var services=new ServiceCollection();
        services.AddLogging();
        services.AddOptions<GeminiOptions>();
        services.AddSingleton(_store);
        services.AddSingleton<IGeminiClient>(_gemini);
        services.AddScoped<PipelineService>();
        _provider=services.BuildServiceProvider();
    }

    //chạy AFTER
    public void Dispose()
    {
        _provider.Dispose();
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helper: project của Owner + file sách
    private async Task<Project> SeedProjectAsync()
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail=Owner,
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, "The Mole had been working very hard all the morning.");
        return project;
    }

    //helper: giả lập 1 HTTP request: scope riêng + PipelineService riêng, y như ASP.NET làm cho mỗi request
    private async Task<IResult> PostRunAsync(Guid projectId, int step, string userEmail, string? style=null)
    {
        using var scope=_provider.CreateScope();
        var pipeline=scope.ServiceProvider.GetRequiredService<PipelineService>();
        return await PipelineEndpoints.RunStepAsync(projectId, step, new RunStepRequest(style), userEmail, _store, pipeline, _channel);
    }

    //helper: đọc status code + body lỗi từ IResult mà handler trả
    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode!.Value;
    private static string? ErrorOf(IResult result) => ((result as IValueHttpResult)?.Value as ApiError)?.Error;

    //helper: start worker, đóng channel, chờ worker làm hết job đang nằm trong hàng
    private async Task DrainQueueWithWorkerAsync()
    {
        var worker=new PipelineWorker(_channel, _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PipelineWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        _channel.Writer.Complete();
        await worker.ExecuteTask!;
    }

    //DoD Phase 6 - double-click nút Run chỉ tạo đúng 1 lời gọi Gemini
    //đi trọn: 20 request đồng thời --> endpoint --> claim --> channel --> worker --> pipeline --> Gemini
    [Fact]
    public async Task RunStep_TwentyConcurrentRequests_OnlyOneGeminiCall()
    {
        //Arrange
        var project=await SeedProjectAsync();

        //Act 1: double-click x20
        var results=await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => PostRunAsync(project.Id, 1, Owner)));

        //Assert 1: đúng 1 cái 202; 19 cái còn lại 409 STEP_ALREADY_RUNNING; hàng đợi có đúng 1 job
        Assert.Equal(1, results.Count(r => StatusOf(r)==StatusCodes.Status202Accepted));
        Assert.Equal(19, results.Count(r => StatusOf(r)==StatusCodes.Status409Conflict && ErrorOf(r)=="STEP_ALREADY_RUNNING"));
        Assert.Equal(1, _channel.Reader.Count);

        //Act 2: cho worker chạy hết hàng đợi
        await DrainQueueWithWorkerAsync();

        //Assert 2: Gemini được gọi đúng 1 lần, bước hoàn thành
        //completedSteps=1 cũng chứng minh job mang đúng vé
        Assert.Single(_gemini.JsonRequests);
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.CompletedSteps);
        Assert.Null(reloaded.RunningStep);
    }

    //user khác k chạy được bước trên project của mình
    [Fact]
    public async Task RunStep_ProjectOfAnotherUser_Returns404AndClaimsNothing()
    {
        //Arrange
        var project=await SeedProjectAsync();

        //Act: user lạ gọi run trên project của Owner
        var result=await PostRunAsync(project.Id, 1, "intruder@example.com");

        //Assert: 404, project k bị đánh dấu đang chạy, k có job nào vào hàng
        var reloaded=await _store.GetAsync(project.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
        Assert.Equal("PROJECT_NOT_FOUND", ErrorOf(result));
        Assert.Null(reloaded.RunningStep);
        Assert.Equal(0, _channel.Reader.Count);
    }

    //sai thứ tự --> 409 với error code riêng, và K có job nào vào hàng (k tốn Gemini)
    [Fact]
    public async Task RunStep_WrongOrder_Returns409AndQueuesNothing()
    {
        //Arrange: project mới, B1 chưa chạy
        var project=await SeedProjectAsync();

        //Act: nhảy cóc gọi B2
        var result=await PostRunAsync(project.Id, 2, Owner);

        //Assert
        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(result));
        Assert.Equal("STEP_NOT_AVAILABLE", ErrorOf(result));
        Assert.Equal(0, _channel.Reader.Count);
    }
}
