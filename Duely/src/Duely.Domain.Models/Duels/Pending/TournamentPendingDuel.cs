using Duely.Domain.Models.Tournaments;

namespace Duely.Domain.Models.Duels.Pending;

public sealed class TournamentPendingDuel : PendingDuel
{
    public required Tournament Tournament { get; init; }
    public required User User1 { get; init; }
    public required User User2 { get; init; }
    public required DuelConfiguration? Configuration { get; init; }
    public bool IsAcceptedByUser1 { get; set; }
    public bool IsAcceptedByUser2 { get; set; }
}
