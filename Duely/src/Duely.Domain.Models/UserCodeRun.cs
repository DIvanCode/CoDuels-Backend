namespace Duely.Domain.Models;

public sealed class UserCodeRun
{
    public int Id { get; init; }
    public required Duel Duel { get; init; }
    public required User User { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
    public required string Input { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required SubmissionStatus Status { get; set; }
    public string? Verdict { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? ExecutionId { get; set; }
}
