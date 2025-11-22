namespace Duely.Domain.Models;

public sealed class OutboxMessage
{
    public int Id { get; init; }
    public required OutboxType Type { get; init; }  
    public required string Payload { get; init; }     
    public OutboxStatus Status { get; set; } = OutboxStatus.ToDo;
    public int Retries { get; set; } = 0;
    public DateTime? RetryAt { get; set; }
}

public enum OutboxStatus
{
    ToDo = 0,
    InProgress = 1,
    ToRetry = 2
}

public enum OutboxType
{
    TestSolution = 0,
    SendMessage = 1,
    RunUserCode = 2
}
