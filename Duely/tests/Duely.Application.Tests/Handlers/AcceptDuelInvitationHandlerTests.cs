using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public class AcceptDuelInvitationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new AcceptDuelInvitationHandler(Context, new DuelManager());

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = 999,
            OpponentNickname = "u1"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_inviter_missing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new AcceptDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_invitation_missing()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        await ctx.SaveChangesAsync();

        var handler = new AcceptDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_accepts_invitation_and_assigns_opponents()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        inviter.Rating = 1500;
        var invitee = EntityFactory.MakeUser(2, "invitee");
        invitee.Rating = 1400;
        ctx.Users.AddRange(inviter, invitee);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(inviter.Id, inviter.Rating, DateTime.UtcNow, expectedOpponentId: invitee.Id, configurationId: 10);
        duelManager.AddUser(invitee.Id, invitee.Rating, DateTime.UtcNow);

        var handler = new AcceptDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var waiting = duelManager.GetWaitingUsers();
        waiting.Should().HaveCount(2);
        waiting.Should().ContainSingle(u => u.UserId == inviter.Id && u.ExpectedOpponentId == invitee.Id && u.IsOpponentAssigned);
        waiting.Should().ContainSingle(u => u.UserId == invitee.Id && u.ExpectedOpponentId == inviter.Id && u.IsOpponentAssigned);
    }

    [Fact]
    public async Task NotFound_when_configuration_mismatch()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        inviter.Rating = 1500;
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(inviter.Id, inviter.Rating, DateTime.UtcNow, expectedOpponentId: invitee.Id, configurationId: 10);

        var handler = new AcceptDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
