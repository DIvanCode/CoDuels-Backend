namespace Duely.Domain.Models;

public enum SubmissionStatus
{
    Queued = 0,
    Running = 1,
    Done = 2
}
public sealed class Submission
{
    public int Id { get; set; }
    public required Duel Duel { get; set; }
    public int UserId { get; set; }
    public required string Code { get; set; }
    public required string Language { get; set; }
    public DateTime SubmitTime { get; set; }
    public SubmissionStatus Status { get; set; }
    public string? Verdict { get; set; }
}