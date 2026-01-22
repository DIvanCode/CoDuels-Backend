namespace Duely.Domain.Models;

public sealed class Submission
{
    public int Id { get; init; }
    public required Duel Duel { get; init; }
    public required User User { get; init; }
    public required char TaskKey { get; init; }
    public required string Solution { get; init; }
    public required Language Language { get; init; }
    public required DateTime SubmitTime { get; init; }
    public required SubmissionStatus Status { get; set; }
    public string? Verdict { get; set; }
    public string? Message { get; set; }
    public required bool IsUpsolving { get; init; }
}

public enum SubmissionStatus
{
    Queued = 0,
    Running = 1,
    Done = 2
}
