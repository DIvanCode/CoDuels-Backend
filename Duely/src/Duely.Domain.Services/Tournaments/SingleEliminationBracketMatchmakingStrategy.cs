using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;

namespace Duely.Domain.Services.Tournaments;

public sealed class SingleEliminationBracketMatchmakingStrategy : ITournamentMatchmakingStrategy
{
    private sealed record NodeCandidate(int NodeIndex, int User1Id, int User2Id);

    public TournamentMatchmakingType Type => TournamentMatchmakingType.SingleEliminationBracket;

    public Tournament CreateTournament(
        string name,
        Group group,
        User createdBy,
        DateTime createdAt,
        DuelConfiguration? duelConfiguration,
        IReadOnlyList<User> participants)
    {
        var tournament = new SingleEliminationBracketTournament
        {
            Name = name,
            Status = TournamentStatus.New,
            Group = group,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            MatchmakingType = Type,
            DuelConfiguration = duelConfiguration
        };

        var seeds = Enumerable
            .Range(1, participants.Count)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        for (var i = 0; i < participants.Count; i++)
        {
            tournament.Participants.Add(new TournamentParticipant
            {
                Tournament = tournament,
                User = participants[i],
                Seed = seeds[i]
            });
        }

        Initialize(tournament);
        return tournament;
    }

    public void Initialize(Tournament tournament)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        var participants = tournament.Participants
            .OrderBy(p => p.Seed)
            .ToList();
        bracketTournament.Nodes = [];
        if (participants.Count == 0)
        {
            return;
        }

        InitializeNodes(bracketTournament.Nodes, participants, 0, 0, participants.Count);
    }

    public IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        return bracketTournament.Nodes
            .Where(n => n?.DuelId != null)
            .Select(n => n!.DuelId!.Value)
            .Distinct()
            .ToList();
    }

    public void Sync(Tournament tournament, IReadOnlyDictionary<int, Duel> duelsById)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        var nodes = bracketTournament.Nodes;
        if (nodes.Count == 0)
        {
            return;
        }

        var changed = false;
        var lastInternalIndex = nodes.Count / 2 - 1;
        for (var index = lastInternalIndex; index >= 0; index--)
        {
            var node = nodes[index];
            if (node == null)
            {
                node = new SingleEliminationBracketNode();
                nodes[index] = node;
                changed = true;
            }
            var leftIndex = GetLeftChildIndex(index);
            var rightIndex = GetRightChildIndex(index);
            var leftWinner = GetWinnerUserId(nodes, leftIndex);
            var rightWinner = GetWinnerUserId(nodes, rightIndex);

            if (node.WinnerUserId != null) continue;
            
            if (node.DuelId is { } duelId &&
                duelsById.TryGetValue(duelId, out var duel) &&
                duel is { Status: DuelStatus.Finished, Winner: not null })
            {
                if (node.WinnerUserId != duel.Winner.Id)
                {
                    node.WinnerUserId = duel.Winner.Id;
                    changed = true;
                }
            }
            else if (node.DuelId == null)
            {
                var leftHasContent = HasSubtreeContent(nodes, leftIndex);
                var rightHasContent = HasSubtreeContent(nodes, rightIndex);

                var winnerUserId = leftWinner != null && leftHasContent && !rightHasContent
                    ? leftWinner
                    : rightWinner != null && !leftHasContent && rightHasContent
                        ? rightWinner
                        : null;
                if (node.WinnerUserId != winnerUserId)
                {
                    node.WinnerUserId = winnerUserId;
                    changed = true;
                }
            }
        }

        if (nodes[0]?.WinnerUserId != null)
        {
            tournament.Status = TournamentStatus.Finished;
        }

        if (changed)
        {
            bracketTournament.Nodes = [.. nodes];
        }
    }

    public IReadOnlyList<TournamentPendingDuelCandidate> GetPendingDuelCandidates(
        Tournament tournament,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        return GetNodeCandidates((SingleEliminationBracketTournament)tournament, duelsById)
            .Select(candidate => new TournamentPendingDuelCandidate(candidate.User1Id, candidate.User2Id))
            .ToList();
    }

    public bool HasPendingDuel(
        Tournament tournament,
        TournamentPendingDuel pendingDuel,
        TournamentPendingDuelCandidate candidate)
    {
        return pendingDuel.Tournament.Id == tournament.Id &&
               ((pendingDuel.User1.Id == candidate.User1Id && pendingDuel.User2.Id == candidate.User2Id) ||
                (pendingDuel.User1.Id == candidate.User2Id && pendingDuel.User2.Id == candidate.User1Id));
    }

    public void AttachDuel(Tournament tournament, TournamentPendingDuel pendingDuel, Duel duel)
    {
        var bracketTournament = (SingleEliminationBracketTournament)tournament;
        var candidate = FindNodeCandidateForUsers(bracketTournament, pendingDuel.User1.Id, pendingDuel.User2.Id);
        if (candidate == null)
        {
            return;
        }

        var node = bracketTournament.Nodes[candidate.NodeIndex] ??= new SingleEliminationBracketNode();
        node.DuelId = duel.Id;
        bracketTournament.Nodes = [.. bracketTournament.Nodes];
    }

    private static IReadOnlyList<NodeCandidate> GetNodeCandidates(
        SingleEliminationBracketTournament bracketTournament,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        var nodes = bracketTournament.Nodes;
        var result = new List<NodeCandidate>();
        var lastInternalIndex = nodes.Count / 2 - 1;

        for (var index = lastInternalIndex; index >= 0; index--)
        {
            var node = nodes[index] ??= new SingleEliminationBracketNode();
            var leftIndex = GetLeftChildIndex(index);
            var rightIndex = GetRightChildIndex(index);
            var leftWinner = GetWinnerUserId(nodes, leftIndex);
            var rightWinner = GetWinnerUserId(nodes, rightIndex);

            if (node.DuelId != null &&
                duelsById.TryGetValue(node.DuelId.Value, out var duel) &&
                duel.Status != DuelStatus.Finished)
            {
                continue;
            }

            if (node.DuelId == null &&
                node.WinnerUserId == null &&
                leftWinner != null &&
                rightWinner != null)
            {
                result.Add(new NodeCandidate(index, leftWinner.Value, rightWinner.Value));
            }
        }

        return result;
    }

    private static void InitializeNodes(
        List<SingleEliminationBracketNode?> nodes,
        IReadOnlyList<TournamentParticipant> participants,
        int nodeIndex,
        int tl,
        int tr)
    {
        EnsureCapacity(nodes, nodeIndex);

        if (tl + 1 == tr)
        {
            nodes[nodeIndex] = new SingleEliminationBracketNode
            {
                UserId = participants[tl].User.Id,
                WinnerUserId = participants[tl].User.Id
            };
            return;
        }

        nodes[nodeIndex] = new SingleEliminationBracketNode();

        var tm = (tl + tr) / 2;
        InitializeNodes(nodes, participants, GetLeftChildIndex(nodeIndex), tl, tm);
        InitializeNodes(nodes, participants, GetRightChildIndex(nodeIndex), tm, tr);
    }

    private static void EnsureCapacity(List<SingleEliminationBracketNode?> nodes, int index)
    {
        while (nodes.Count <= index)
        {
            nodes.Add(null);
        }
    }

    private static int GetLeftChildIndex(int index)
    {
        return index * 2 + 1;
    }

    private static int GetRightChildIndex(int index)
    {
        return index * 2 + 2;
    }

    private static int? GetWinnerUserId(IReadOnlyList<SingleEliminationBracketNode?> nodes, int index)
    {
        return index < nodes.Count ? nodes[index]?.WinnerUserId : null;
    }

    private static bool HasSubtreeContent(IReadOnlyList<SingleEliminationBracketNode?> nodes, int index)
    {
        if (index >= nodes.Count)
        {
            return false;
        }

        var node = nodes[index];
        if (node is { UserId: not null } or { DuelId: not null } or { WinnerUserId: not null })
        {
            return true;
        }

        return HasSubtreeContent(nodes, GetLeftChildIndex(index)) ||
               HasSubtreeContent(nodes, GetRightChildIndex(index));
    }

    private static NodeCandidate? FindNodeCandidateForUsers(
        SingleEliminationBracketTournament tournament,
        int user1Id,
        int user2Id)
    {
        var candidates = GetNodeCandidates(tournament, new Dictionary<int, Duel>());
        return candidates.SingleOrDefault(candidate =>
            (candidate.User1Id == user1Id && candidate.User2Id == user2Id) ||
            (candidate.User1Id == user2Id && candidate.User2Id == user1Id));
    }
}
