namespace Duely.Domain.Models.Duels.Pending;

public sealed class RankedPendingDuel : PendingDuel
{
    public required User User { get; init; }
    public required int Rating { get; init; }
    public required DateTime EnqueuedAt { get; init; }
}
