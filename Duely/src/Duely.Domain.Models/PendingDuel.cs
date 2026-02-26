namespace Duely.Domain.Models;

public abstract class PendingDuel
{
    public int Id { get; init; }
    public required PendingDuelType Type { get; init; }
}

public enum PendingDuelType
{
    Ranked = 1,
    Friendly = 2,
    Group = 3
}
