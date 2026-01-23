using Duely.Domain.Models.Outbox.Payloads;

namespace Duely.Domain.Models.Outbox;

public sealed class OutboxMessage
{
    public int Id { get; init; }
    public OutboxType Type { get; init; }  
    public required OutboxPayload Payload { get; init; }     
    public OutboxStatus Status { get; set; } = OutboxStatus.ToDo;
    public required DateTime RetryUntil { get; init; }
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
