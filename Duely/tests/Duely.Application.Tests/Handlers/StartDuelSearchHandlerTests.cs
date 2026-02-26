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

public class StartDuelSearchHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new StartDuelSearchHandler(Context);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_when_user_already_in_rated_search_does_not_add_duplicate()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1400;
        ctx.Users.Add(u1);
        ctx.PendingDuels.Add(new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = u1,
            Rating = u1.Rating,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>()
            .Count(p => p.User.Id == u1.Id)
            .Should().Be(1);
    }

    [Fact]
    public async Task Success_adds_ranked_pending()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1400;
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>()
            .Should().ContainSingle(p => p.User.Id == u1.Id);

        var outbox = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_cancels_friendly_invitation_and_adds_to_rated_search()
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

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = inviter.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().BeEmpty();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().ContainSingle(p => p.User.Id == inviter.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().OnlyContain(m => m.Type == OutboxType.SendMessage);

        var inviteePayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == invitee.Id));
        inviteePayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(inviter.Nickname);

        var inviterPayload = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == inviter.Id));
        inviterPayload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(invitee.Nickname);
    }

    [Fact]
    public async Task AlreadyExists_when_user_has_active_duel()
    {
        var ctx = Context;
        var user = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(user);
        ctx.Duels.Add(new Duely.Domain.Models.Duels.Duel
        {
            Id = user.Id,
            Status = Duely.Domain.Models.Duels.DuelStatus.InProgress,
            Configuration = new Duely.Domain.Models.Duels.DuelConfiguration
            {
                Id = 1,
                Owner = null,
                IsRated = true,
                ShouldShowOpponentSolution = true,
                MaxDurationMinutes = 30,
                TasksCount = 1,
                TasksOrder = Duely.Domain.Models.Duels.DuelTasksOrder.Sequential,
                TasksConfigurations = []
            },
            Tasks = [],
            User1 = user,
            User1InitRating = user.Rating,
            User2 = user,
            User2InitRating = user.Rating,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1Solutions = [],
            User2Solutions = []
        });
        await ctx.SaveChangesAsync();

        var handler = new StartDuelSearchHandler(ctx);

        var res = await handler.Handle(new StartDuelSearchCommand
        {
            UserId = user.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }
}
