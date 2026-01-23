using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class DenyDuelInvitationHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new DenyDuelInvitationHandler(Context, new DuelManager());

        var res = await handler.Handle(new DenyDuelInvitationCommand
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

        var handler = new DenyDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new DenyDuelInvitationCommand
        {
            UserId = 1,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_invitation_missing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        var handler = new DenyDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new DenyDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_removes_invitation_and_sends_cancel_event()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u2.Id, u2.Rating, DateTime.UtcNow, expectedOpponentId: u1.Id);

        var handler = new DenyDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new DenyDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().BeEmpty();

        var outboxMessage = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = ReadSendPayload(outboxMessage);
        payload.UserId.Should().Be(u2.Id);
        payload.Message.Should().BeOfType<DuelInvitationDeniedMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
    }
}
