using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Tournaments.SingleEliminationBracketTournaments;

public sealed class SingleEliminationBracketTournament : Tournament
{
    public required List<SingleEliminationBracketNode?> Nodes { get; init; }
}

public enum SingleEliminationBracketNodeType
{
    Defined = 0,
    Undefined = 1
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SingleEliminationBracketDefinedNode), nameof(SingleEliminationBracketNodeType.Defined))]
[JsonDerivedType(typeof(SingleEliminationBracketUndefinedNode), nameof(SingleEliminationBracketNodeType.Undefined))]
public abstract class SingleEliminationBracketNode
{
    public required int Index { get; init; }
}

public sealed class SingleEliminationBracketDefinedNode : SingleEliminationBracketNode
{
    public required int UserId { get; init; }
}

public sealed class SingleEliminationBracketUndefinedNode : SingleEliminationBracketNode
{
    public required int DuelId { get; init; }
}
