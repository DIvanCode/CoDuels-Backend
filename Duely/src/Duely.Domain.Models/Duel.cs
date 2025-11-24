namespace Duely.Domain.Models;

public sealed class Duel
{
    public int Id { get; init; }
    public required string TaskId { get; init; }
    public required User? User1 { get; init; }
    public required User? User2 { get; init; }
    public required DuelStatus Status { get; set; }
    public required DateTime StartTime { get; init; }
    public required DateTime DeadlineTime { get; init; }
    public DateTime? EndTime { get; set; }
    public User? Winner { get; set; }
    public List<Submission> Submissions { get; set; } = [];
    public int? User1RatingDelta { get; set; }
    public int? User2RatingDelta { get; set; }
}

public enum DuelStatus
{
    InProgress = 0,
    Finished = 1
}
