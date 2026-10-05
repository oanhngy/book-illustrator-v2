namespace server.Pipeline;

public enum ClaimStatus
{
    NotFound, //404, not found
    AlreadyRunning, //409, double lick/2 tab
    WrongOrder, //409
    Claimed //202, thành công
}

//kqua trả về của ClaimStepAsync, record vì test so sánh=gtri đc
public record ClaimResult(ClaimStatus Status, DateTime? Ticket);