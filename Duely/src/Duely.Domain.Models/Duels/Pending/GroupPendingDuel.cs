using Duely.Domain.Models.Groups;

namespace Duely.Domain.Models.Duels.Pending;

public sealed class GroupPendingDuel : PendingDuel
{
    public required Group Group { get; init; }
    public required User CreatedBy { get; init; }
    public required User User1 { get; init; }
    public required User User2 { get; init; }
    public DuelConfiguration? Configuration { get; init; }
    public bool IsAcceptedByUser1 { get; set; } = false;
    public bool IsAcceptedByUser2 { get; set; } = false;
}
