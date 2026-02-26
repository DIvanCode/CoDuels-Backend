using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class AcceptDuelInvitationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new AcceptDuelInvitationHandler(Context);

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

        var handler = new AcceptDuelInvitationHandler(ctx);

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

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_accepts_invitation()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = inviter,
            User2 = invitee,
            Configuration = new DuelConfiguration
            {
                Id = 10,
                Owner = inviter,
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

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().ContainSingle(p => p.IsAccepted);
    }

    [Fact]
    public async Task NotFound_when_configuration_mismatch()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = inviter,
            User2 = invitee,
            Configuration = new DuelConfiguration
            {
                Id = 10,
                Owner = inviter,
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

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task AlreadyExists_when_user_has_active_duel()
    {
        var ctx = Context;
        var user = EntityFactory.MakeUser(1, "u1");
        var opponent = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(user, opponent);
        ctx.Duels.Add(new Duel
        {
            Status = DuelStatus.InProgress,
            Configuration = new DuelConfiguration
            {
                Id = 1,
                Owner = null,
                IsRated = true,
                ShouldShowOpponentSolution = true,
                MaxDurationMinutes = 30,
                TasksCount = 1,
                TasksOrder = DuelTasksOrder.Sequential,
                TasksConfigurations = []
            },
            Tasks = [],
            User1 = user,
            User1InitRating = user.Rating,
            User2 = opponent,
            User2InitRating = opponent.Rating,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1Solutions = [],
            User2Solutions = []
        });
        await ctx.SaveChangesAsync();

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = user.Id,
            OpponentNickname = opponent.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task Cancels_ranked_pending_in_pending_duels()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        ctx.Users.AddRange(inviter, invitee);
        ctx.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = invitee,
                Rating = invitee.Rating,
                CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = inviter,
                User2 = invitee,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            });
        await ctx.SaveChangesAsync();

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();

        var outboxMessage = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = (SendMessagePayload)outboxMessage.Payload;
        payload.UserId.Should().Be(invitee.Id);
        payload.Message.Should().BeOfType<DuelSearchCanceledMessage>();
    }

    [Fact]
    public async Task Cancels_outgoing_friendly_invitation_when_accepting()
    {
        var ctx = Context;
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invitee = EntityFactory.MakeUser(2, "invitee");
        var other = EntityFactory.MakeUser(3, "other");
        ctx.Users.AddRange(inviter, invitee, other);
        ctx.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = invitee,
                User2 = other,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = inviter,
                User2 = invitee,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            });
        await ctx.SaveChangesAsync();

        var handler = new AcceptDuelInvitationHandler(ctx);

        var res = await handler.Handle(new AcceptDuelInvitationCommand
        {
            UserId = invitee.Id,
            OpponentNickname = inviter.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().ContainSingle(p => p.User1.Id == inviter.Id && p.User2.Id == invitee.Id && p.IsAccepted);
        ctx.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().NotContain(p => p.User1.Id == invitee.Id && p.User2.Id == other.Id);

        var outboxMessage = await ctx.OutboxMessages.AsNoTracking().SingleAsync();
        outboxMessage.Type.Should().Be(OutboxType.SendMessage);
        var payload = (SendMessagePayload)outboxMessage.Payload;
        payload.UserId.Should().Be(invitee.Id);
        payload.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(other.Nickname);
    }
}
