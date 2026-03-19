using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Tournaments;
using FluentAssertions;

namespace Duely.Domain.Tests;

public sealed class SingleEliminationBracketMatchmakingStrategyTests
{
    private readonly SingleEliminationBracketMatchmakingStrategy _strategy = new();

    [Fact]
    public void CreateTournament_with_one_participant_creates_single_root_leaf()
    {
        var group = new Group { Id = 1, Name = "g" };
        var creator = MakeUser(1, "creator");
        var user = MakeUser(2, "u1");

        var tournament = (SingleEliminationBracketTournament)_strategy.CreateTournament(
            "Cup",
            group,
            creator,
            DateTime.UtcNow,
            null,
            new[] { user });

        tournament.Status.Should().Be(TournamentStatus.New);
        tournament.Nodes.Should().HaveCount(1);
        tournament.Nodes[0]!.UserId.Should().Be(user.Id);
        tournament.Nodes[0]!.WinnerUserId.Should().Be(user.Id);
    }

    [Fact]
    public void Sync_with_one_participant_finishes_tournament_without_pending_duels()
    {
        var tournament = CreateTournament(1);

        _strategy.Sync(tournament, new Dictionary<int, Duel>());
        var candidates = _strategy.GetPendingDuelCandidates(tournament, new Dictionary<int, Duel>());

        tournament.Status.Should().Be(TournamentStatus.Finished);
        tournament.Nodes[0]!.WinnerUserId.Should().Be(1);
        candidates.Should().BeEmpty();
    }

    [Fact]
    public void Sync_with_three_participants_advances_bye_only_in_local_subtree()
    {
        var tournament = CreateTournament(3);

        _strategy.Sync(tournament, new Dictionary<int, Duel>());
        var candidates = _strategy.GetPendingDuelCandidates(tournament, new Dictionary<int, Duel>());

        tournament.Status.Should().NotBe(TournamentStatus.Finished);
        tournament.Nodes.Should().HaveCount(7);
        tournament.Nodes[0]!.WinnerUserId.Should().BeNull();
        candidates.Should().ContainSingle();
        candidates[0].User1Id.Should().BeOneOf(2, 3);
        candidates[0].User2Id.Should().BeOneOf(2, 3);
        candidates[0].User1Id.Should().NotBe(candidates[0].User2Id);
    }

    [Fact]
    public void GetPendingDuelCandidates_with_power_of_two_participants_returns_first_round_matches()
    {
        var tournament = CreateTournament(4);

        _strategy.Sync(tournament, new Dictionary<int, Duel>());
        var candidates = _strategy.GetPendingDuelCandidates(tournament, new Dictionary<int, Duel>());

        candidates.Should().HaveCount(2);
        candidates.Should().Contain(c => c.User1Id == 1 && c.User2Id == 2);
        candidates.Should().Contain(c => c.User1Id == 3 && c.User2Id == 4);
        tournament.Nodes[0]!.WinnerUserId.Should().BeNull();
    }

    [Fact]
    public void Sync_with_power_of_two_plus_one_participants_does_not_finish_early()
    {
        var tournament = CreateTournament(5);

        _strategy.Sync(tournament, new Dictionary<int, Duel>());
        var candidates = _strategy.GetPendingDuelCandidates(tournament, new Dictionary<int, Duel>());

        tournament.Status.Should().NotBe(TournamentStatus.Finished);
        tournament.Nodes.Should().HaveCount(15);
        tournament.Nodes[0]!.WinnerUserId.Should().BeNull();
        candidates.Should().HaveCount(2);
        candidates.Should().Contain(c => c.User1Id == 1 && c.User2Id == 2);
        candidates.Should().Contain(c => c.User1Id == 4 && c.User2Id == 5);
    }

    [Fact]
    public void Sync_propagates_finished_duel_winner_to_parent_candidate()
    {
        var tournament = CreateTournament(3);
        var winner = tournament.Participants.Single(p => p.User.Id == 3).User;
        var loser = tournament.Participants.Single(p => p.User.Id == 2).User;
        var duel = MakeFinishedDuel(10, loser, winner, winner);

        tournament.Nodes[2]!.DuelId = duel.Id;
        var duelsById = new Dictionary<int, Duel> { [duel.Id] = duel };

        _strategy.Sync(tournament, duelsById);
        var candidates = _strategy.GetPendingDuelCandidates(tournament, duelsById);

        tournament.Nodes[2]!.WinnerUserId.Should().Be(winner.Id);
        candidates.Should().ContainSingle();
        candidates[0].User1Id.Should().Be(1);
        candidates[0].User2Id.Should().Be(winner.Id);
    }

    [Fact]
    public void GetPendingDuelCandidates_does_not_return_node_with_in_progress_duel()
    {
        var tournament = CreateTournament(4);
        var user1 = tournament.Participants.Single(p => p.User.Id == 1).User;
        var user2 = tournament.Participants.Single(p => p.User.Id == 2).User;
        var duel = MakeInProgressDuel(20, user1, user2);
        tournament.Nodes[1]!.DuelId = duel.Id;

        var candidates = _strategy.GetPendingDuelCandidates(
            tournament,
            new Dictionary<int, Duel> { [duel.Id] = duel });

        candidates.Should().HaveCount(1);
        candidates[0].User1Id.Should().Be(3);
        candidates[0].User2Id.Should().Be(4);
    }

    private SingleEliminationBracketTournament CreateTournament(int participantsCount)
    {
        var group = new Group { Id = 1, Name = "g" };
        var creator = MakeUser(100, "creator");
        var participants = Enumerable.Range(1, participantsCount)
            .Select(i => MakeUser(i, $"u{i}"))
            .ToArray();
        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.New,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        for (var i = 0; i < participants.Length; i++)
        {
            tournament.Participants.Add(new TournamentParticipant
            {
                Tournament = tournament,
                User = participants[i],
                Seed = i + 1
            });
        }

        _strategy.Initialize(tournament);
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
