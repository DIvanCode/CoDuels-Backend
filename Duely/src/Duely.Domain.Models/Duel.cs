namespace Duely.Domain.Models;

public enum DuelStatus
{
    InProgress = 0,
    Finished = 1
}
public enum DuelResult
{
    None = 0,
    Draw = 1,
    User1 = 2,
    User2 = 3
}

public sealed class Duel
{
    public int Id { get; set; }
    public required string TaskId { get; set; }
    public int User1Id { get; set; }
    public int User2Id { get; set; }
    public DuelStatus Status { get; set; }
    public DuelResult Result { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int MaxDuration { get; set; } = 30;
}