using Duely.Application.UseCases.Helpers;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using FluentAssertions;

namespace Duely.Application.Tests.Helpers;

public sealed class TournamentDetailsMapperTests
{
    private readonly SingleEliminationBracketTournamentDetailsMapper _mapper = new();

    [Fact]
    public void Resolver_returns_matching_mapper()
    {
        var resolver = new TournamentDetailsMapperResolver([_mapper]);

        var result = resolver.GetMapper(TournamentMatchmakingType.SingleEliminationBracket);

        result.Should().BeSameAs(_mapper);
    }

    [Fact]
    public void Resolver_throws_when_mapper_is_missing()
    {
        var resolver = new TournamentDetailsMapperResolver([]);

        var act = () => resolver.GetMapper(TournamentMatchmakingType.SingleEliminationBracket);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetReferencedUserIds_returns_distinct_participants_and_winners()
    {
        var tournament = CreateTournament();
        tournament.Nodes = [
            new SingleEliminationBracketNode { WinnerUserId = 3 },
            new SingleEliminationBracketNode { UserId = 1, WinnerUserId = 1 },
            new SingleEliminationBracketNode { UserId = 2, WinnerUserId = 3 }
        ];

        var userIds = _mapper.GetReferencedUserIds(tournament);

        userIds.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void GetReferencedDuelIds_returns_distinct_non_null_ids()
    {
        var tournament = CreateTournament();
        tournament.Nodes = [
            new SingleEliminationBracketNode { DuelId = 10 },
            new SingleEliminationBracketNode { DuelId = 10 },
            new SingleEliminationBracketNode { DuelId = 11 }
        ];

        var duelIds = _mapper.GetReferencedDuelIds(tournament);

        duelIds.Should().BeEquivalentTo([10, 11]);
    }

    [Fact]
    public void MapDetails_maps_bracket_and_sorts_participants_by_seed()
    {
        var creator = EntityFactory.MakeUser(10, "creator");
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var winner = EntityFactory.MakeUser(3, "u3");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 7,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Nodes = [
            new SingleEliminationBracketNode { DuelId = 20, WinnerUserId = winner.Id },
            new SingleEliminationBracketNode { UserId = user1.Id, WinnerUserId = user1.Id },
            null
        ];

        var duel = new Duel
        {
            Id = 20,
            Status = DuelStatus.InProgress,
            Configuration = CreateConfiguration(creator),
            Tasks = new Dictionary<char, DuelTask> { ['A'] = new("TASK-1", 1, []) },
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = user1,
            User1InitRating = user1.Rating,
            User2 = user2,
            User2InitRating = user2.Rating
        };

        var dto = _mapper.MapDetails(
            tournament,
            new Dictionary<int, Duely.Domain.Models.User>
            {
                [creator.Id] = creator,
                [user1.Id] = user1,
                [user2.Id] = user2,
                [winner.Id] = winner
            },
            new Dictionary<int, Duel> { [duel.Id] = duel });

        dto.Tournament.Participants.Select(p => p.Id).Should().Equal(user1.Id, user2.Id);
        dto.SingleEliminationBracket.Should().NotBeNull();
        dto.SingleEliminationBracket!.Nodes[0]!.DuelId.Should().Be(20);
        dto.SingleEliminationBracket.Nodes[0]!.DuelStatus.Should().Be(DuelStatus.InProgress);
        dto.SingleEliminationBracket.Nodes[0]!.LeftIndex.Should().Be(1);
        dto.SingleEliminationBracket.Nodes[0]!.RightIndex.Should().BeNull();
        dto.SingleEliminationBracket.Nodes[0]!.Winner!.Id.Should().Be(winner.Id);
        dto.SingleEliminationBracket.Nodes[2].Should().BeNull();
    }

    private static SingleEliminationBracketTournament CreateTournament()
    {
        var creator = EntityFactory.MakeUser(10, "creator");
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.New,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });
        return tournament;
    }

    private static DuelConfiguration CreateConfiguration(Duely.Domain.Models.User owner)
    {
        return new DuelConfiguration
        {
            Id = 99,
            Owner = owner,
            IsRated = false,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] }
            }
        };
    }
}
