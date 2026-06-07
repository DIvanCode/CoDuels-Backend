using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments.Entities.Configurations;

public sealed class SingleEliminationBracketTournamentConfiguration : TournamentConfiguration
{
    internal SingleEliminationBracketTournamentConfiguration(DuelConfiguration duelConfiguration)
        : base(TournamentConfigurationType.SingleEliminationBracket, duelConfiguration)
    {
    }

    private List<SingleEliminationBracketNode> _nodes = [];
    public IReadOnlyList<SingleEliminationBracketNode> Nodes => _nodes.AsReadOnly();
    
    internal override void Build(IReadOnlyCollection<TournamentParticipant> participants)
    {
        var orderedParticipants = new List<TournamentParticipant>(participants.Count);
        orderedParticipants.AddRange(participants.OrderBy(p => p.Seed));

        for (var v = 0; v < 4 * orderedParticipants.Count; v++)
        {
            _nodes.Add(new SingleEliminationBracketNullNode(v, v * 2 + 1, v * 2 + 2));
        }
        
        _buildNodes(0, 0, orderedParticipants.Count, orderedParticipants);
    }
    
    private void _buildNodes(int v, int tl, int tr, List<TournamentParticipant> participants)
    {
        if (tl + 1 == tr)
        {
            _nodes[v] = new SingleEliminationBracketUserNode(
                index: v, 
                leftChildIndex: null, 
                rightChildIndex: null,
                userId: participants[tl].User.Id);
            return;
        }

        var tm = (tl + tr) / 2;
        
        _buildNodes(v * 2 + 1, tl, tm, participants);
        _buildNodes(v * 2 + 2, tm, tr, participants);

        _nodes[v] = new SingleEliminationBracketDuelNode(
            index: v,
            leftChildIndex: v * 2 + 1,
            rightChildIndex: v * 2 + 2,
            duelId: null);
    }
}

public enum SingleEliminationBracketNodeType
{
    Null = 0,
    User = 1,
    Duel = 2
}

public abstract class SingleEliminationBracketNode : ValueObject
{
    protected SingleEliminationBracketNode(
        SingleEliminationBracketNodeType type,
        int index,
        int? leftChildIndex,
        int? rightChildIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        
        if (leftChildIndex.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(leftChildIndex.Value);   
        }
        
        if (rightChildIndex.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(rightChildIndex.Value);
        }
        
        Type = type;
        Index = index;
        LeftChildIndex = leftChildIndex;
        RightChildIndex = rightChildIndex;
    }
    
    public SingleEliminationBracketNodeType Type { get; init; }
    public int Index { get; init; }
    public int? LeftChildIndex { get; init; }
    public int? RightChildIndex { get; init; }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Index;
    }
}

public sealed class SingleEliminationBracketNullNode : SingleEliminationBracketNode
{
    public SingleEliminationBracketNullNode(int index, int? leftChildIndex, int? rightChildIndex)
        : base(SingleEliminationBracketNodeType.User, index, leftChildIndex, rightChildIndex)
    {
    }
}

public sealed class SingleEliminationBracketUserNode : SingleEliminationBracketNode
{
    public SingleEliminationBracketUserNode(int index, int? leftChildIndex, int? rightChildIndex, UserId userId)
        : base(SingleEliminationBracketNodeType.User, index, leftChildIndex, rightChildIndex)
    {
        UserId = userId;
    }
    
    public UserId UserId { get; init; }
}

public sealed class SingleEliminationBracketDuelNode : SingleEliminationBracketNode
{
    public SingleEliminationBracketDuelNode(int index, int? leftChildIndex, int? rightChildIndex, DuelId? duelId)
        : base(SingleEliminationBracketNodeType.Duel, index, leftChildIndex, rightChildIndex)
    {
        DuelId = duelId;
    }
    
    public DuelId? DuelId { get; private set; }

    public void SetDuelId(DuelId duelId)
    {
        DuelId = duelId;
    }
}
