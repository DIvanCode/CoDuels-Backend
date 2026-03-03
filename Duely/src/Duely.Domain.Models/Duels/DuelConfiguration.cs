namespace Duely.Domain.Models.Duels;

public enum DuelTasksOrder
{
    Sequential = 0,
    Parallel = 1
}

public sealed class DuelConfiguration
{
    public int Id { get; init; }
    public User? Owner { get; init; }
    public bool IsRated { get; set; }
    public bool ShouldShowOpponentSolution { get; set; }
    public int MaxDurationMinutes { get; set; }
    public int TasksCount { get; set; }
    public DuelTasksOrder TasksOrder { get; set; }
    public Dictionary<char, DuelTaskConfiguration> TasksConfigurations { get; set; } = [];
}

public sealed class DuelTaskConfiguration
{
    public required int Level { get; set; }
    public required string[] Topics { get; set; }
}
