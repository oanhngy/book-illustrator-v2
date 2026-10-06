namespace server.Pipeline;

//endpoint-->Channel<T>-->worker-->RunStepAsync (A5 bước 6-9)
public record StepJob(Guid ProjectId, int Step, DateTime Ticket);