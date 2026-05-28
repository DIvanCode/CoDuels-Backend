namespace Duely.Domain.Models.Duels;

public sealed class DuelConfiguration
{
    public int Id { get; init; }
    public User? Owner { get; init; }
    public bool IsDeleted { get; set; }

    public required bool IsRated { get; init; }
    public required bool ShouldShowOpponentSolution { get; set; }
    public required int MaxDurationMinutes { get; set; }
    public required int TasksCount { get; set; }
    public required DuelTasksOrder TasksOrder { get; set; }
}

public enum DuelTasksOrder
{
    Sequential = 0,
    Parallel = 1
}
