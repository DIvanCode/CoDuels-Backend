using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class FinishTournamentHandlerTests : ContextBasedTest
{
    [Theory]
    [InlineData(GroupRole.Creator, false, true)]
    [InlineData(GroupRole.Manager, false, true)]
    [InlineData(GroupRole.Member, false, false)]
    [InlineData(GroupRole.Member, true, true)]
    public async Task Enforces_roles_and_admin_override(GroupRole role, bool isAdmin, bool allowed)
    {
        var actor = EntityFactory.MakeUser(1, "actor");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { Group = group, User = actor, Role = role });
        var tournament = MakeTournament(group, actor);
        Context.Users.Add(actor);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        var result = await FinishAsync(actor.Id, tournament.Id, isAdmin);

        result.IsSuccess.Should().Be(allowed);
        if (!allowed) result.Errors.Should().ContainSingle(e => e is ForbiddenError);
        (await Context.Tournaments.SingleAsync()).Status.Should()
            .Be(allowed ? TournamentStatus.Finished : TournamentStatus.InProgress);
    }

    [Fact]
    public async Task Admin_without_membership_can_finish()
    {
        var admin = EntityFactory.MakeUser(1, "admin");
        var owner = EntityFactory.MakeUser(2, "owner");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var tournament = MakeTournament(group, owner);
        Context.Users.AddRange(admin, owner);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        (await FinishAsync(admin.Id, tournament.Id, true)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Pending_manager_is_forbidden()
    {
        var actor = EntityFactory.MakeUser(1, "actor");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = actor, Group = group, Role = GroupRole.Manager, InvitationPending = true
        });
        var tournament = MakeTournament(group, actor);
        Context.Users.Add(actor);
        Context.Groups.Add(group);
        Context.Tournaments.Add(tournament);
        await Context.SaveChangesAsync();

        (await FinishAsync(actor.Id, tournament.Id, false)).Errors
            .Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Deletes_only_own_pending_preserves_active_duel_and_is_idempotent()
    {
        var owner = EntityFactory.MakeUser(1, "owner");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership { Group = group, User = owner, Role = GroupRole.Creator });
        var tournament = MakeTournament(group, owner);
        var other = MakeTournament(group, owner);
        other.Name = "Other";
        var duel = EntityFactory.MakeDuel(100, user1, user2);
        tournament.DuelIds = [duel.Id];

        Context.Users.AddRange(owner, user1, user2);
        Context.Groups.Add(group);
        Context.Tournaments.AddRange(tournament, other);
        Context.Duels.Add(duel);
        Context.PendingDuels.AddRange(
            MakePending(tournament, user1, user2),
            MakePending(tournament, user2, user1),
            MakePending(other, user1, user2));
        await Context.SaveChangesAsync();

        (await FinishAsync(owner.Id, tournament.Id, false)).Value.Status.Should().Be(TournamentStatus.Finished);
        (await Context.PendingDuels.OfType<TournamentPendingDuel>().CountAsync()).Should().Be(1);
        var remaining = await Context.PendingDuels.OfType<TournamentPendingDuel>()
            .Include(p => p.Tournament).SingleAsync();
        remaining.Tournament.Id.Should().Be(other.Id);
        (await Context.Duels.SingleAsync()).Status.Should().Be(DuelStatus.InProgress);
        (await Context.Tournaments.OfType<GroupStageTournament>()
            .SingleAsync(t => t.Id == tournament.Id)).DuelIds.Should().Contain(duel.Id);
        (await Context.Tournaments.SingleAsync(t => t.Id == other.Id)).Status
            .Should().Be(TournamentStatus.InProgress);

        (await FinishAsync(owner.Id, tournament.Id, false)).IsSuccess.Should().BeTrue();
        (await Context.Duels.SingleAsync()).Status.Should().Be(DuelStatus.InProgress);
    }

    [Fact]
    public async Task Unknown_tournament_is_not_found()
    {
        (await FinishAsync(1, 999, true)).Errors
            .Should().ContainSingle(e => e is EntityNotFoundError);
    }

    private Task<FluentResults.Result<Duely.Application.UseCases.Dtos.TournamentDto>> FinishAsync(
        int userId, int tournamentId, bool isAdmin)
        => new FinishTournamentHandler(Context, new GroupPermissionsService())
            .Handle(new FinishTournamentCommand
            {
                UserId = userId,
                TournamentId = tournamentId,
                IsAdmin = isAdmin
            }, CancellationToken.None);

    private static GroupStageTournament MakeTournament(Group group, User owner) => new()
    {
        Name = "Cup",
        Group = group,
        CreatedBy = owner,
        CreatedAt = DateTime.UtcNow,
        Status = TournamentStatus.InProgress,
        MatchmakingType = TournamentMatchmakingType.GroupStage
    };

    private static TournamentPendingDuel MakePending(Tournament tournament, User u1, User u2) => new()
    {
        Type = PendingDuelType.Tournament,
        Tournament = tournament,
        User1 = u1,
        User2 = u2,
        Configuration = null,
        CreatedAt = DateTime.UtcNow
    };
}
