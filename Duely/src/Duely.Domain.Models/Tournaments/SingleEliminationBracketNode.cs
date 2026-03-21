namespace Duely.Domain.Models.Tournaments;

public sealed class SingleEliminationBracketNode
{
    public int? UserId { get; set; }
    public int? DuelId { get; set; }
    public int? WinnerUserId { get; set; }
}
