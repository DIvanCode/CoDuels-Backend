using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public sealed class GetGroupTournamentsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_group_tournaments_ordered_by_creation_time()
    {
        var viewer = EntityFactory.MakeUser(1, "viewer");
        var creator = EntityFactory.MakeUser(2, "creator");
        var user1 = EntityFactory.MakeUser(3, "u1");
        var user2 = EntityFactory.MakeUser(4, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = viewer, Group = group, Role = GroupRole.Member });

        var olderTournament = new SingleEliminationBracketTournament
        {
            Name = "Older",
            Status = TournamentStatus.New,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        olderTournament.Participants.Add(new TournamentParticipant { Tournament = olderTournament, User = user1, Seed = 1 });
        olderTournament.Participants.Add(new TournamentParticipant { Tournament = olderTournament, User = user2, Seed = 2 });

        var newerTournament = new SingleEliminationBracketTournament
        {
            Name = "Newer",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        newerTournament.Participants.Add(new TournamentParticipant { Tournament = newerTournament, User = user2, Seed = 1 });
        newerTournament.Participants.Add(new TournamentParticipant { Tournament = newerTournament, User = user1, Seed = 2 });

        Context.Users.AddRange(viewer, creator, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.AddRange(olderTournament, newerTournament);
        await Context.SaveChangesAsync();

        var handler = new GetGroupTournamentsHandler(Context, new GroupPermissionsService());
        var result = await handler.Handle(new GetGroupTournamentsQuery
        {
            UserId = viewer.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Name).Should().Equal("Newer", "Older");
    }

    [Fact]
    public async Task Returns_forbidden_for_pending_membership()
    {
        var invited = EntityFactory.MakeUser(1, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true
        });

        Context.Users.Add(invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new GetGroupTournamentsHandler(Context, new GroupPermissionsService());
        var result = await handler.Handle(new GetGroupTournamentsQuery
        {
            UserId = invited.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
