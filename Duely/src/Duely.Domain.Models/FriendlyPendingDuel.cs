namespace Duely.Domain.Models;

public sealed class FriendlyPendingDuel : PendingDuel
{
    public required User User1 { get; init; }
    public required User User2 { get; init; }
    public DuelConfiguration? Configuration { get; init; }
    public bool IsAccepted { get; set; } = false;
}
