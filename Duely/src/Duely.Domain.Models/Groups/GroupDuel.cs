using Duely.Domain.Models.Duels;

namespace Duely.Domain.Models.Groups;

public sealed class GroupDuel
{
    public required Group Group { get; init; }
    public required Duel Duel { get; init; }
    public required User CreatedBy { get; init; }
}
