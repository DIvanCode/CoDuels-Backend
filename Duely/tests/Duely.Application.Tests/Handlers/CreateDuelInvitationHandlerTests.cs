using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Duels.Pending;
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
        var handler = new CreateDuelInvitationHandler(Context);

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

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task AlreadyExists_when_user_has_active_duel()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.Duels.Add(EntityFactory.MakeDuel(10, u1, u2));
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task AlreadyExists_when_opponent_has_active_duel()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        ctx.Users.AddRange(u1, u2, u3);
        ctx.Duels.Add(EntityFactory.MakeDuel(10, u2, u3));
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task Forbidden_when_inviting_self()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u1.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task NotFound_when_configuration_missing()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_removes_ranked_search_and_sends_invitation()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1200;
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new RankedPendingDuel
        {
            Type = PendingDuelType.Ranked,
            User = u1,
            Rating = u1.Rating,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle(p =>
            p.User1.Id == u1.Id && p.User2.Id == u2.Id && !p.IsAccepted);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().OnlyContain(m => m.Type == OutboxType.SendMessage);

        var canceled = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u1.Id));
        canceled.Message.Should().BeOfType<DuelSearchCanceledMessage>();

        var invitation = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u2.Id));
        invitation.Message.Should().BeOfType<DuelInvitationMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
    }

    [Fact]
    public async Task Success_when_outgoing_invitation_to_same_opponent_exists_does_nothing()
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
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().ContainSingle(p => p.User1.Id == u1.Id && p.User2.Id == u2.Id);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
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
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u3.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle(p =>
            p.User1.Id == u1.Id && p.User2.Id == u3.Id && !p.IsAccepted);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(3);

        var canceled = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u1.Id));
        canceled.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u2.Nickname);

        var canceledOpponent = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u2.Id));
        canceledOpponent.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);

        var invitationMessage = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == u3.Id));
        invitationMessage.Message.Should().BeOfType<DuelInvitationMessage>()
            .Which.OpponentNickname.Should().Be(u1.Nickname);
    }

    [Fact]
    public async Task Success_when_opponent_already_invited_user()
    {
        var ctx = Context;
        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1300;
        var u2 = EntityFactory.MakeUser(2, "u2");
        u2.Rating = 1250;
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u2,
            User2 = u1,
            Configuration = null,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelInvitationHandler(ctx);

        var res = await handler.Handle(new CreateDuelInvitationCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().HaveCount(2);
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle(p =>
            p.User1.Id == u2.Id && p.User2.Id == u1.Id && !p.IsAccepted);
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().ContainSingle(p =>
            p.User1.Id == u1.Id && p.User2.Id == u2.Id && !p.IsAccepted);

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(1);
    }
}
