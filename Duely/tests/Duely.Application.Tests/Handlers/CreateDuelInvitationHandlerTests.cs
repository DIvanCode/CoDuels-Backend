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

public class CreateDuelInvitationHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CreateDuelInvitationHandler(Context, new DuelManager());

        var res = await handler.Handle(new CreateDuelInvitationCommand
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

        var handler = new CreateDuelInvitationHandler(ctx, new DuelManager());

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_removes_rated_search_and_sends_invitation()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1200;
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u1.Id, u1.Rating, DateTime.UtcNow);

        var handler = new CreateDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().ContainSingle(u =>
            u.UserId == u1.Id && u.ExpectedOpponentId == u2.Id);

        var outboxMessage = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = ReadSendPayload(outboxMessage);
        payload.UserId.Should().Be(u2.Id);
        payload.Message.Should().BeOfType<DuelInvitationMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
    }

    [Fact]
    public async Task Success_cancels_previous_friendly_invitation_and_sends_new_invitation()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1300;
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u1.Id, u1.Rating, DateTime.UtcNow, expectedOpponentId: u2.Id);

        var handler = new CreateDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u3.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().ContainSingle(u =>
            u.UserId == u1.Id && u.ExpectedOpponentId == u3.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        var canceledMessage = ReadSendPayload(outboxMessages.Single(m =>
            ((SendMessagePayload)m.Payload).Message is DuelInvitationCanceledMessage));
        canceledMessage.UserId.Should().Be(u2.Id);
        canceledMessage.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);

        var invitationMessage = ReadSendPayload(outboxMessages.Single(m =>
            ((SendMessagePayload)m.Payload).Message is DuelInvitationMessage));
        invitationMessage.UserId.Should().Be(u3.Id);
        invitationMessage.Message.Should().BeOfType<DuelInvitationMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
    }

    [Fact]
    public async Task Conflict_when_opponent_already_invited_user()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1300;
        var u2 = EntityFactory.MakeUser(2, "u2");
        u2.Rating = 1250;
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u2.Id, u2.Rating, DateTime.UtcNow, expectedOpponentId: u1.Id);

        var handler = new CreateDuelInvitationHandler(ctx, duelManager);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
        duelManager.GetWaitingUsers().Should().ContainSingle(u =>
            u.UserId == u2.Id && u.ExpectedOpponentId == u1.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }
}
