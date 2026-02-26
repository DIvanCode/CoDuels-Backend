using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
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
        var handler = new CancelDuelInvitationHandler(Context);

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

        var handler = new CancelDuelInvitationHandler(ctx);

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
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = new DuelConfiguration
            {
                Id = 10,
                Owner = u1,
                MaxDurationMinutes = 30,
                IsRated = true,
                ShouldShowOpponentSolution = false,
                TasksCount = 1,
                TasksOrder = DuelTasksOrder.Sequential,
                TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
                {
                    ['A'] = new()
                    {
                        Level = 1,
                        Topics = []
                    }
                }
            },
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().BeEmpty();

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

        var handler = new CancelDuelInvitationHandler(ctx);

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

    [Fact]
    public async Task Cancels_even_when_invitation_already_accepted()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CancelDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().BeEmpty();

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().OnlyContain(m => m.Type == OutboxType.SendMessage);

        var opponentPayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u2.Id));
        opponentPayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);

        var senderPayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u1.Id));
        senderPayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u2.Nickname);
    }
}
