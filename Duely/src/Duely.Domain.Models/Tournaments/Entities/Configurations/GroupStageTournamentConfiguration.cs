using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments.Entities.Configurations;

public sealed class GroupStageTournamentConfiguration : TournamentConfiguration
{
    internal GroupStageTournamentConfiguration(DuelConfiguration duelConfiguration)
        : base(TournamentConfigurationType.GroupStage, duelConfiguration)
    {
    }

    private List<GroupStageNode> _nodes = [];
    public IReadOnlyList<GroupStageNode> Nodes => _nodes.AsReadOnly();
    
    internal override void Build(IReadOnlyCollection<TournamentParticipant> participants)
    {
        _nodes = participants
            .OrderBy(p => p.Seed)
            .Select((p, i) => new GroupStageNode(i, p.User.Id))
            .ToList();
    }
}

public sealed class GroupStageNode : ValueObject
{
    public GroupStageNode(int index, UserId userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        Index = index;
        UserId = userId;
    }
    
    public int Index { get; init; }
    public UserId UserId { get; init; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Index;
        yield return UserId;
    }
}
