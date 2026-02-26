using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Search;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Duels.Pending;
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
        var handler = new CancelDuelSearchHandler(Context);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_removes_ranked_pending()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1200;
        ctx.Users.Add(u1);
        ctx.PendingDuels.Add(new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = u1,
            Rating = u1.Rating,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelSearchHandler(ctx);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();

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
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = inviter,
            User2 = invitee,
            Configuration = null,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelSearchHandler(ctx);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = inviter.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle(p => p.User1.Id == inviter.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_when_ranked_pending_missing_does_nothing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelSearchHandler(ctx);

        var res = await handler.Handle(new CancelDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();
        var outbox = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }
}
