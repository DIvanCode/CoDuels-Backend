using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public sealed class GetTournamentHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_hydrated_single_elimination_bracket()
    {
        var viewer = EntityFactory.MakeUser(1, "viewer");
        var creator = EntityFactory.MakeUser(2, "creator");
        var user1 = EntityFactory.MakeUser(3, "u1");
        var user2 = EntityFactory.MakeUser(4, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = viewer, Group = group, Role = GroupRole.Member });

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });
        tournament.Nodes = new List<SingleEliminationBracketNode?>
        {
            new SingleEliminationBracketNode(),
            new SingleEliminationBracketNode { UserId = user1.Id, WinnerUserId = user1.Id },
            new SingleEliminationBracketNode { UserId = user2.Id, WinnerUserId = user2.Id }
        };

        Context.Users.AddRange(viewer, creator, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var handler = new GetTournamentHandler(
            Context,
            new GroupPermissionsService(),
            new TournamentDetailsMapperResolver(new ITournamentDetailsMapper[]
            {
                new SingleEliminationBracketTournamentDetailsMapper()
            }));
        var result = await handler.Handle(new GetTournamentQuery
        {
            UserId = viewer.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tournament.Id.Should().Be(tournament.Id);
        result.Value.SingleEliminationBracket.Should().NotBeNull();
        result.Value.SingleEliminationBracket!.Nodes.Should().HaveCount(3);
        result.Value.SingleEliminationBracket.Nodes[1]!.User!.Id.Should().Be(user1.Id);
        result.Value.SingleEliminationBracket.Nodes[2]!.User!.Id.Should().Be(user2.Id);
    }

    [Fact]
    public async Task Returns_null_child_indexes_for_missing_nodes()
    {
        var viewer = EntityFactory.MakeUser(1, "viewer");
        var creator = EntityFactory.MakeUser(2, "creator");
        var user1 = EntityFactory.MakeUser(3, "u1");
        var user2 = EntityFactory.MakeUser(4, "u2");
        var user3 = EntityFactory.MakeUser(5, "u3");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = viewer, Group = group, Role = GroupRole.Member });

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Nodes = new List<SingleEliminationBracketNode?>
            {
                new(),
                new() { UserId = user1.Id, WinnerUserId = user1.Id },
                new(),
                null,
                null,
                new() { UserId = user2.Id, WinnerUserId = user2.Id },
                new() { UserId = user3.Id, WinnerUserId = user3.Id }
            }
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user3, Seed = 3 });

        Context.Users.AddRange(viewer, creator, user1, user2, user3);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var handler = new GetTournamentHandler(
            Context,
            new GroupPermissionsService(),
            new TournamentDetailsMapperResolver(new ITournamentDetailsMapper[]
            {
                new SingleEliminationBracketTournamentDetailsMapper()
            }));

        var result = await handler.Handle(new GetTournamentQuery
        {
            UserId = viewer.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SingleEliminationBracket.Should().NotBeNull();
        result.Value.SingleEliminationBracket!.Nodes[1]!.LeftIndex.Should().BeNull();
        result.Value.SingleEliminationBracket.Nodes[1]!.RightIndex.Should().BeNull();
        result.Value.SingleEliminationBracket.Nodes[2]!.LeftIndex.Should().Be(5);
        result.Value.SingleEliminationBracket.Nodes[2]!.RightIndex.Should().Be(6);
    }
}
