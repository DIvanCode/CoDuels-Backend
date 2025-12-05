using System.ComponentModel;
using System.Dynamic;
using System.Runtime.CompilerServices;

namespace Duely.Domain.Models;

public sealed class UserCodeRun
{
    public int Id { get; init; }
    public required User User { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
    public required string Input { get; init; }
    public required UserCodeRunStatus Status { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? ExecutionId { get; set; }
    public required DateTime CreatedAt { get; init; }
}

public enum UserCodeRunStatus
{
    Queued = 0,
    Running = 1,
    Done = 2
}
