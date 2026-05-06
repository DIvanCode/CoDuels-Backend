namespace Duely.Domain.Models.Tournaments;

public sealed class GroupStageTournament : Tournament
{
    public List<int> DuelIds { get; set; } = [];
}
