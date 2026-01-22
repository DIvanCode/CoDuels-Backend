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

public class CancelDuelInvitationHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CancelDuelInvitationHandler(Context, new DuelManager());

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = 999,
            OpponentNickname = "u2"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_opponent_missing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_cancels_invitation_and_sends_message()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1400;
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u1.Id, u1.Rating, DateTime.UtcNow, expectedOpponentId: u2.Id, configurationId: 10);

        var handler = new CancelDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().BeEmpty();

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().OnlyContain(m => m.Type == OutboxType.SendMessage);

        var opponentPayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u2.Id));
        opponentPayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
        ((DuelInvitationCanceledMessage)opponentPayload.Message).ConfigurationId.Should().Be(10);

        var senderPayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u1.Id));
        senderPayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u2.Nickname);
        ((DuelInvitationCanceledMessage)senderPayload.Message).ConfigurationId.Should().Be(10);
    }

    [Fact]
    public async Task Success_when_invitation_missing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }
}
