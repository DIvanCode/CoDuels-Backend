namespace Duely.Domain.Models;

public sealed class Duel
{
    public int Id { get; init; }
    
    public required DuelStatus Status { get; set; }
    public required DuelConfiguration Configuration { get; init; }
    public required Dictionary<char, DuelTask> Tasks { get; init; }
    public Dictionary<char, DuelTaskSolution> User1Solutions { get; set; } = [];
    public Dictionary<char, DuelTaskSolution> User2Solutions { get; set; } = [];
    
    public required DateTime StartTime { get; init; }
    public required DateTime DeadlineTime { get; init; }
    public DateTime? EndTime { get; set; }
    
    public required User User1 { get; init; }
    public required int User1InitRating { get; init; }
    public int? User1FinalRating { get; set; }
    public required User User2 { get; init; }
    public required int User2InitRating { get; init; }
    public int? User2FinalRating { get; set; }
    public User? Winner { get; set; }
    
    public List<Submission> Submissions { get; init; } = [];
}

public enum DuelStatus
{
    InProgress = 1,
    Finished = 2
}

public enum DuelResult
{
    Win = 0,
    Lose = 1,
    Draw = 2
}
