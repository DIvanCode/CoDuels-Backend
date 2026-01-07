namespace Duely.Domain.Models;

public sealed class Duel
{
    public int Id { get; init; }
    
    public required DuelStatus Status { get; set; }
    public required DuelConfiguration Configuration { get; init; }
    public Dictionary<char, DuelTask> Tasks { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime DeadlineTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public required User User1 { get; init; }
    public int User1InitRating { get; set; }
    public int? User1FinalRating { get; set; }
    public required User User2 { get; init; }
    public int User2InitRating { get; set; }
    public int? User2FinalRating { get; set; }
    public User? Winner { get; set; }
    
    public List<Submission> Submissions { get; set; } = [];
}

public enum DuelStatus
{
    Pending = 0,
    InProgress = 1,
    Finished = 2
}

public enum DuelResult
{
    Win = 0,
    Lose = 1,
    Draw = 2
}
