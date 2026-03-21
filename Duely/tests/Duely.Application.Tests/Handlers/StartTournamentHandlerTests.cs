using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Application.Services.Errors;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class StartTournamentHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_tournament_dto_with_participants()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = creator, Group = group, Role = GroupRole.Creator });
        group.Users.Add(new GroupMembership { User = user1, Group = group, Role = GroupRole.Member });
        group.Users.Add(new GroupMembership { User = user2, Group = group, Role = GroupRole.Member });

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

        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        var handler = new StartTournamentHandler(Context, new GroupPermissionsService());
        var result = await handler.Handle(new StartTournamentCommand
        {
            UserId = creator.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TournamentStatus.InProgress);
        result.Value.Participants.Select(p => p.Nickname).Should().Equal(user1.Nickname, user2.Nickname);

        var storedTournament = await Context.Tournaments.SingleAsync();
        storedTournament.Status.Should().Be(TournamentStatus.InProgress);
    }

    [Fact]
    public async Task Returns_forbidden_for_member_without_permissions()
    {
        var member = EntityFactory.MakeUser(1, "member");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { User = member, Group = group, Role = GroupRole.Member });

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.New,
            Group = group,
            CreatedBy = member,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });

        Context.Users.AddRange(member, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var handler = new StartTournamentHandler(Context, new GroupPermissionsService());
        var result = await handler.Handle(new StartTournamentCommand
        {
            UserId = user1.Id,
            TournamentId = tournament.Id
        }, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
