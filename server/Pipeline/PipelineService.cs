using server.Storage;
namespace server.Pipeline;

public class PipelineService
{
    private readonly ProjectStore _store;
    public PipelineService(ProjectStore store)
    {
        _store=store;
    }

    public async Task<ClaimResult> ClaimStepAsync(Guid projectId, int step, string? requestedStyle)
    {
        //2 biến ngoài lambda
        var status=ClaimStatus.NotFound;
        DateTime? ticket=null;

    await _store.UpdateAsync(projectId, p =>
    {
        if(p.RunningStep is not null)
        {
            status=ClaimStatus.AlreadyRunning;
            return false;
        }    

        if(p.CompletedSteps != step-1)
        {
            status=ClaimStatus.WrongOrder;
            return false;
        }

        var now=DateTime.UtcNow;
        p.RunningStep=step;
        p.RunningSince=now;
        //claim lại bước đã lỗi=retry
        p.FailedStep=null;
        p.LastError=null;

        if(step==1)
        {
            p.RequestedStyle=requestedStyle;
        }

        status=ClaimStatus.Claimed;
        ticket=now;
        return true; //true=UpdateAsync ghi atomic xuống đĩa
    });
    return new ClaimResult(status, ticket);
    }
}