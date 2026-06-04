using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.TournamentDuels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Tournaments.Entities.GroupStageTournaments;
using Duely.Domain.Models.Users;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Services.Tournaments;

public sealed class GroupStageMatchmakingStrategy : ITournamentMatchmakingStrategy
{
    public TournamentMatchmakingType Type => TournamentMatchmakingType.GroupStage;

    public Tournament CreateTournament(
        string name,
        Group group,
        User createdBy,
        DateTime createdAt,
        DuelConfiguration? duelConfiguration,
        IReadOnlyList<User> participants)
    {
        var tournament = new GroupStageTournament
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
        ((GroupStageTournament)tournament).DuelIds = [];
    }

    public IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament)
    {
        return ((GroupStageTournament)tournament).DuelIds
            .Distinct()
            .ToList();
    }

    public void Sync(Tournament tournament, IReadOnlyDictionary<int, Duel> duelsById)
    {
        var expectedDuelsCount = GetExpectedDuelsCount(tournament.Participants.Count);
        var duelIds = ((GroupStageTournament)tournament).DuelIds.Distinct().ToList();
        if (duelIds.Count == expectedDuelsCount &&
            duelIds.All(id => duelsById.TryGetValue(id, out var duel) && duel.Status == DuelStatus.Finished))
        {
            tournament.Status = TournamentStatus.Finished;
        }
    }

    public IReadOnlyList<TournamentPendingDuelCandidate> GetPendingDuelCandidates(
        Tournament tournament,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        var playedPairs = duelsById.Values
            .Select(duel => GetPairKey(duel.User1.Id, duel.User2.Id))
            .ToHashSet();

        var participants = tournament.Participants
            .OrderBy(p => p.Seed)
            .Select(p => p.User.Id)
            .ToList();

        var candidates = new List<TournamentPendingDuelCandidate>();
        for (var i = 0; i < participants.Count; i++)
        {
            for (var j = i + 1; j < participants.Count; j++)
            {
                var user1Id = participants[i];
                var user2Id = participants[j];
                if (playedPairs.Contains(GetPairKey(user1Id, user2Id)))
                {
                    continue;
                }

                candidates.Add(new TournamentPendingDuelCandidate(user1Id, user2Id));
            }
        }

        return candidates;
    }

    public bool HasPendingDuel(
        Tournament tournament,
        TournamentPendingDuel pendingDuel,
        TournamentPendingDuelCandidate candidate)
    {
        return pendingDuel.Tournament.Id == tournament.Id &&
               GetPairKey(pendingDuel.User1.Id, pendingDuel.User2.Id) ==
               GetPairKey(candidate.User1Id, candidate.User2Id);
    }

    public void AttachDuel(Tournament tournament, TournamentPendingDuel pendingDuel, Duel duel)
    {
        var groupStageTournament = (GroupStageTournament)tournament;
        if (groupStageTournament.DuelIds.Contains(duel.Id))
        {
            return;
        }

        groupStageTournament.DuelIds = [.. groupStageTournament.DuelIds, duel.Id];
    }

    private static int GetExpectedDuelsCount(int participantsCount)
    {
        return participantsCount * (participantsCount - 1) / 2;
    }

    private static string GetPairKey(int user1Id, int user2Id)
    {
        return user1Id < user2Id
            ? $"{user1Id}:{user2Id}"
            : $"{user2Id}:{user1Id}";
    }
}
