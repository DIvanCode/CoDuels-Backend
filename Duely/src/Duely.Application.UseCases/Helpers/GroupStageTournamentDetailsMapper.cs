using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;

namespace Duely.Application.UseCases.Helpers;

public sealed class GroupStageTournamentDetailsMapper : ITournamentDetailsMapper
{
    public TournamentMatchmakingType Type => TournamentMatchmakingType.GroupStage;

    public IReadOnlyCollection<int> GetReferencedUserIds(Tournament tournament)
    {
        return tournament.Participants.Select(p => p.User.Id).ToList();
    }

    public IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament)
    {
        return ((GroupStageTournament)tournament).DuelIds
            .Distinct()
            .ToList();
    }

    public TournamentDetailsDto MapDetails(
        Tournament tournament,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, Duel> duelsById)
    {
        var participantIds = tournament.Participants
            .OrderBy(p => p.Seed)
            .Select(p => p.User.Id)
            .ToList();
        var standingsByUserId = participantIds.ToDictionary(
            id => id,
            id => new StandingAccumulator(id));

        foreach (var duel in duelsById.Values.Where(duel => duel.Status == DuelStatus.Finished))
        {
            if (!standingsByUserId.ContainsKey(duel.User1.Id) ||
                !standingsByUserId.ContainsKey(duel.User2.Id))
            {
                continue;
            }

            ApplyDuelResult(standingsByUserId[duel.User1.Id], duel, duel.User1.Id);
            ApplyDuelResult(standingsByUserId[duel.User2.Id], duel, duel.User2.Id);
        }

        var mappedDuels = ((GroupStageTournament)tournament).DuelIds
            .Distinct()
            .Select(id => duelsById.TryGetValue(id, out var duel) ? duel : null)
            .Where(duel => duel != null)
            .Select(duel => MapDuel(duel!))
            .ToList();

        return new TournamentDetailsDto
        {
            Tournament = TournamentDtoMapper.Map(tournament),
            GroupStage = new GroupStageDto
            {
                Standings = standingsByUserId.Values
                    .Select(acc => new GroupStageStandingDto
                    {
                        User = usersById.TryGetValue(acc.UserId, out var user)
                            ? MapUser(user)
                            : MapUser(tournament.Participants.Single(p => p.User.Id == acc.UserId).User),
                        Wins = acc.Wins,
                        Draws = acc.Draws,
                        Losses = acc.Losses,
                        Points = acc.Points
                    })
                    .OrderByDescending(s => s.Points)
                    .ThenByDescending(s => s.Wins)
                    .ThenByDescending(s => s.Draws)
                    .ThenBy(s => s.User.Nickname)
                    .ToList(),
                CurrentDuels = mappedDuels
                    .Where(duel => duel.Status == DuelStatus.InProgress)
                    .OrderByDescending(duel => duel.StartTime)
                    .ToList(),
                PastDuels = mappedDuels
                    .Where(duel => duel.Status == DuelStatus.Finished)
                    .OrderByDescending(duel => duel.StartTime)
                    .ToList()
            }
        };
    }

    private static void ApplyDuelResult(StandingAccumulator standing, Duel duel, int userId)
    {
        if (duel.Winner is null)
        {
            standing.Draws++;
            standing.Points++;
            return;
        }

        if (duel.Winner.Id == userId)
        {
            standing.Wins++;
            standing.Points += 3;
            return;
        }

        standing.Losses++;
    }

    private static GroupStageDuelDto MapDuel(Duel duel)
    {
        return new GroupStageDuelDto
        {
            Id = duel.Id,
            User1 = MapUser(duel.User1),
            User2 = MapUser(duel.User2),
            WinnerId = duel.Winner?.Id,
            Status = duel.Status,
            StartTime = duel.StartTime,
            DeadlineTime = duel.DeadlineTime,
            EndTime = duel.EndTime
        };
    }

    private static UserDto MapUser(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Nickname = user.Nickname,
            Rating = user.Rating,
            CreatedAt = user.CreatedAt
        };
    }

    private sealed class StandingAccumulator(int userId)
    {
        public int UserId { get; } = userId;
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int Points { get; set; }
    }
}
