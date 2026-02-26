namespace Duely.Domain.Models;

public sealed class RankedPendingDuel : PendingDuel
{
    public required User User { get; init; }
    public required int Rating { get; init; }
    public required DateTime CreatedAt { get; init; }
}
