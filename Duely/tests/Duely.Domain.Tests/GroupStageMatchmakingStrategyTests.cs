using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Tournaments;
using FluentAssertions;

namespace Duely.Domain.Tests;

public sealed class GroupStageMatchmakingStrategyTests
{
    private readonly GroupStageMatchmakingStrategy _strategy = new();

    [Fact]
    public void GetPendingDuelCandidates_returns_all_unplayed_pairs()
    {
        var tournament = CreateTournament(4);

        var candidates = _strategy.GetPendingDuelCandidates(tournament, new Dictionary<int, Duel>());

        candidates.Should().HaveCount(6);
        candidates.Should().Contain(c => c.User1Id == 1 && c.User2Id == 2);
        candidates.Should().Contain(c => c.User1Id == 1 && c.User2Id == 3);
        candidates.Should().Contain(c => c.User1Id == 1 && c.User2Id == 4);
        candidates.Should().Contain(c => c.User1Id == 2 && c.User2Id == 3);
        candidates.Should().Contain(c => c.User1Id == 2 && c.User2Id == 4);
        candidates.Should().Contain(c => c.User1Id == 3 && c.User2Id == 4);
    }

    [Fact]
    public void GetPendingDuelCandidates_excludes_pairs_with_existing_duel()
    {
        var tournament = CreateTournament(3);
        var user1 = tournament.Participants.Single(p => p.User.Id == 1).User;
        var user2 = tournament.Participants.Single(p => p.User.Id == 2).User;
        var duel = MakeInProgressDuel(10, user1, user2);
        tournament.DuelIds.Add(duel.Id);

        var candidates = _strategy.GetPendingDuelCandidates(
            tournament,
            new Dictionary<int, Duel> { [duel.Id] = duel });

        candidates.Should().HaveCount(2);
        candidates.Should().NotContain(c => c.User1Id == 1 && c.User2Id == 2);
    }

    [Fact]
    public void Sync_finishes_when_every_pair_has_finished_duel()
    {
        var tournament = CreateTournament(3);
        var user1 = tournament.Participants.Single(p => p.User.Id == 1).User;
        var user2 = tournament.Participants.Single(p => p.User.Id == 2).User;
        var user3 = tournament.Participants.Single(p => p.User.Id == 3).User;
        var duels = new[]
        {
            MakeFinishedDuel(10, user1, user2, user1),
            MakeFinishedDuel(11, user1, user3, user3),
            MakeFinishedDuel(12, user2, user3, user2)
        };
        tournament.DuelIds = duels.Select(d => d.Id).ToList();
        tournament.Status = TournamentStatus.InProgress;

        _strategy.Sync(tournament, duels.ToDictionary(d => d.Id));

        tournament.Status.Should().Be(TournamentStatus.Finished);
    }

    [Fact]
    public void AttachDuel_adds_duel_id_once()
    {
        var tournament = CreateTournament(2);
        var user1 = tournament.Participants.Single(p => p.User.Id == 1).User;
        var user2 = tournament.Participants.Single(p => p.User.Id == 2).User;
        var duel = MakeInProgressDuel(10, user1, user2);

        _strategy.AttachDuel(tournament, null!, duel);
        _strategy.AttachDuel(tournament, null!, duel);

        tournament.DuelIds.Should().Equal(10);
    }

    private GroupStageTournament CreateTournament(int participantsCount)
    {
        var group = new Group { Id = 1, Name = "g" };
        var creator = MakeUser(100, "creator");
        var participants = Enumerable.Range(1, participantsCount)
            .Select(i => MakeUser(i, $"u{i}"))
            .ToArray();
        var tournament = (GroupStageTournament)_strategy.CreateTournament(
            "Group",
            group,
            creator,
            DateTime.UtcNow,
            null,
            participants);

        tournament.Participants.Clear();
        for (var i = 0; i < participants.Length; i++)
        {
            tournament.Participants.Add(new TournamentParticipant
            {
                Tournament = tournament,
                User = participants[i],
                Seed = i + 1
            });
        }

        return tournament;
    }

    private static User MakeUser(int id, string nickname)
    {
        return new User
        {
            Id = id,
            Nickname = nickname,
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = 1500,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Duel MakeFinishedDuel(int id, User user1, User user2, User winner)
    {
        var duel = MakeInProgressDuel(id, user1, user2);
        duel.Status = DuelStatus.Finished;
        duel.Winner = winner;
        duel.EndTime = DateTime.UtcNow;
        return duel;
    }

    private static Duel MakeInProgressDuel(int id, User user1, User user2)
    {
        return new Duel
        {
            Id = id,
            Status = DuelStatus.InProgress,
            Configuration = new DuelConfiguration
            {
                Id = id,
                Owner = user1,
                IsRated = false,
                ShouldShowOpponentSolution = true,
                MaxDurationMinutes = 30,
                TasksCount = 1,
                TasksOrder = DuelTasksOrder.Sequential,
                TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
                {
                    ['A'] = new()
                    {
                        Level = 1,
                        Topics = []
                    }
                }
            },
            Tasks = new Dictionary<char, DuelTask>
            {
                ['A'] = new DuelTask("TASK-1", 1, [])
            },
            User1Solutions = [],
            User2Solutions = [],
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = user1,
            User1InitRating = user1.Rating,
            User2 = user2,
            User2InitRating = user2.Rating
        };
    }
}
