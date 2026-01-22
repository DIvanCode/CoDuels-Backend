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

public class CancelDuelSearchHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CancelDuelSearchHandler(Context, new DuelManager());

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_removes_user_from_rated_search()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1200;
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(u1.Id, u1.Rating, DateTime.UtcNow);

        var handler = new CancelDuelSearchHandler(ctx, duelManager);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().BeEmpty();

        var outboxMessage = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = ReadSendPayload(outboxMessage);
        payload.UserId.Should().Be(u1.Id);
        payload.Message.Should().BeOfType<DuelSearchCanceledMessage>();
    }

    [Fact]
    public async Task Success_does_not_remove_friendly_invitation()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        inviter.Rating = 1500;
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        duelManager.AddUser(inviter.Id, inviter.Rating, DateTime.UtcNow, expectedOpponentId: invitee.Id);

        var handler = new CancelDuelSearchHandler(ctx, duelManager);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = inviter.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.GetWaitingUsers().Should().ContainSingle(u =>
            u.UserId == inviter.Id && u.ExpectedOpponentId == invitee.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }
}
