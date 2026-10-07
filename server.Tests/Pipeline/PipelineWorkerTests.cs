using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using server.Gemini;
using server.Models;
using server.Pipeline;
using server.Storage;
namespace server.Tests.Pipeline;

public class PipelineWorkerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectStore _store;
    private readonly StubGeminiClient _gemini;
    private readonly Channel<StepJob> _channel;
    private readonly ServiceProvider _provider;
    private readonly PipelineWorker _worker;

    //before
    public PipelineWorkerTests()
    {
        _tempDir=Path.Combine(Path.GetTempPath(), $"book-illustrator-test-{Guid.NewGuid()}");
        _store=new ProjectStore(_tempDir);
        _gemini=new StubGeminiClient();
        _channel=Channel.CreateUnbounded<StepJob>();

        var services=new ServiceCollection();
        services.AddLogging(); //cho ILogger<PipelineService>
        services.AddOptions<GeminiOptions>(); //cho IOptions<GeminiOptions>
        services.AddSingleton(_store);
        services.AddSingleton<IGeminiClient>(_gemini);
        services.AddScoped<PipelineService>(); //cùng lifetime với Program.cs
        _provider=services.BuildServiceProvider();

        _worker=new PipelineWorker(_channel, _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PipelineWorker>.Instance);
    }

    //after
    public void Dispose()
    {
        _provider.Dispose();
        if(Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive:true);
        }
    }

    //helper: tạo project + sách, claim B1 thật, trả job (CHƯA bỏ vào channel)
    private async Task<StepJob> SeedClaimedStepOneAsync()
    {
        var project=new Project
        {
            Id=Guid.NewGuid(),
            UserEmail="test@example.com",
            Title="Test Project",
            CreatedAt=DateTime.UtcNow,
        };
        await _store.SaveAsync(project);
        await _store.SaveBookTextAsync(project.Id, "The Mole had been working very hard all the morning.");

        using var scope=_provider.CreateScope();
        var claim=await scope.ServiceProvider.GetRequiredService<PipelineService>().ClaimStepAsync(project.Id, 1, null);
        return new StepJob(project.Id, 1, claim.Ticket!.Value);
    }

    //helper: start worker, đóng channel (k nhận thêm job), rồi CHỜ worker xử lý hết những job đã nằm trong hàng
    //Complete() làm ReadAllAsync kết thúc sau job cuối --> ExecuteAsync return --> chờ được, k cần Task.Delay đoán mò
    private async Task RunWorkerUntilQueueDrainedAsync()
    {
        await _worker.StartAsync(CancellationToken.None);
        _channel.Writer.Complete();
        await _worker.ExecuteTask!;
    }

    //job cvao2 channel chạy tới đích (channel --> worker --> scope --> pipeline --> disk); TUẦN TỰ, kbh gọi Gemini cùng lúc
    [Fact]
    public async Task Worker_TwoQueuedJobs_RunsBothOneAtATime()
    {
        //Arrange: 2 job của 2 project; stub đếm số lời gọi đang chạy cùng lúc và ghi lại mức cao nhất
        var jobA=await SeedClaimedStepOneAsync();
        var jobB=await SeedClaimedStepOneAsync();
        var running=0;
        var maxRunning=0;
        _gemini.OnJson=async _ =>
        {
            var now=Interlocked.Increment(ref running);
            maxRunning=Math.Max(maxRunning, now);
            await Task.Delay(50); //giữ lời gọi "đang chạy" 1 lúc: nếu worker song song thì 2 lời gọi sẽ chồng nhau ở đây
            Interlocked.Decrement(ref running);
            return StubGeminiClient.JsonResult("""{ "style": "stub style" }""", "stub-interaction-1");
        };
        _channel.Writer.TryWrite(jobA);
        _channel.Writer.TryWrite(jobB);

        //Act
        await RunWorkerUntilQueueDrainedAsync();

        //Assert: cả 2 project xong B1, và chưa lúc nào có quá 1 lời gọi Gemini cùng chạy
        var a=await _store.GetAsync(jobA.ProjectId);
        var b=await _store.GetAsync(jobB.ProjectId);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(1, a.CompletedSteps);
        Assert.Equal(1, b.CompletedSteps);
        Assert.Equal(1, maxRunning);
    }

    //lỗi 1 bước, các bước sau vẫn chạy
    [Fact]
    public async Task Worker_FirstJobFails_StillRunsNextJob()
    {
        //Arrange: lời gọi Gemini đầu tiên ném lỗi, lời gọi thứ 2 bình thường
        var jobA=await SeedClaimedStepOneAsync();
        var jobB=await SeedClaimedStepOneAsync();
        var calls=0;
        _gemini.OnJson=_ =>
        {
            calls++;
            if(calls==1) throw new GeminiException("Gemini returned 503", 503);
            return Task.FromResult(StubGeminiClient.JsonResult("""{ "style": "stub style" }""", "stub-interaction-1"));
        };
        _channel.Writer.TryWrite(jobA);
        _channel.Writer.TryWrite(jobB);

        //Act
        await RunWorkerUntilQueueDrainedAsync();

        //Assert: A thành bước lỗi (retry được), B vẫn hoàn thành
        var a=await _store.GetAsync(jobA.ProjectId);
        var b=await _store.GetAsync(jobB.ProjectId);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(1, a.FailedStep);
        Assert.Null(a.RunningStep);
        Assert.Equal(1, b.CompletedSteps);
    }

}