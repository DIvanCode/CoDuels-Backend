namespace Duely.Domain.Models.Tournaments;

public sealed class SingleEliminationBracketTournament : Tournament
{
    public List<SingleEliminationBracketNode?> Nodes { get; set; } = [];
}
