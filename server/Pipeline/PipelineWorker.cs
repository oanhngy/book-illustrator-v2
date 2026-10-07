using System.Threading.Channels;
namespace server.Pipeline;

//worker chạy nền: lấy từng job khỏi Channel r chạy
//BackgroundService=class nền
public class PipelineWorker : BackgroundService
{
    private readonly Channel<StepJob> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PipelineWorker> _logger;

    public PipelineWorker(Channel<StepJob> channel, IServiceScopeFactory scopeFactory, ILogger<PipelineWorker> logger)
    {
        _channel=channel;
        _scopeFactory=scopeFactory;
        _logger=logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //ReadAllAsync=rỗng thì ngủ (k tốn CPU), có job nhả ra
        //await foreach + await bên trong=tuần tự
        await foreach(var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                //mỗi job 1 scope riêng, hết job--> dispose
                using var scope=_scopeFactory.CreateScope();
                var pipeline=scope.ServiceProvider.GetRequiredService<PipelineService>();
                await pipeline.RunStepAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker failed on step {Step} of project {ProjectId}", job.Step, job.ProjectId);
            }
        }
    }
}