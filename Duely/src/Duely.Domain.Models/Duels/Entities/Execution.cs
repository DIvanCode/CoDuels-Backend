using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels;

public sealed class Execution
{
    public int Id { get; init; }
    public required User Owner { get; init; }
    public required Duel Duel { get; init; }
    public required char TaskKey { get; init; }
    
    public required string Text { get; set; }
    public required Language Language { get; set; }
    public required string Input { get; init; }
    
    public required ExecutionStatus Status { get; set; }
    public required DateTime CreatedAt { get; init; }
    
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? ExternalExecutionId { get; set; }
    public int LastHandledStatusSeqId { get; set; } = 0;
}

public enum ExecutionStatus
{
    New = 0,
    Queued = 1,
    Running = 2,
    Done = 3
}
