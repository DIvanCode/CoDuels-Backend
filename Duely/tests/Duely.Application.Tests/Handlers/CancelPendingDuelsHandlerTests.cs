using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Tournaments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class CancelPendingDuelsHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage message)
        => (SendMessagePayload)message.Payload;

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CancelPendingDuelsHandler(Context);

        var res = await handler.Handle(new CancelPendingDuelsCommand
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

        var handler = new CancelPendingDuelsHandler(ctx);

        var res = await handler.Handle(new CancelPendingDuelsCommand
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();

        var outbox = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_removes_outgoing_friendly_and_sends_canceled_messages()
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
            Configuration = null,
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelPendingDuelsHandler(ctx);

        var res = await handler.Handle(new CancelPendingDuelsCommand
        {
            UserId = inviter.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>().Should().BeEmpty();

        var outboxMessages = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().OnlyContain(m => m.Type == OutboxType.SendMessage);

        var forInviter = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == inviter.Id));
        forInviter.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(invitee.Nickname);

        var forInvitee = ReadSendPayload(outboxMessages.Single(m => ReadSendPayload(m).UserId == invitee.Id));
        forInvitee.Message.Should().BeOfType<DuelInvitationCanceledMessage>()
            .Which.OpponentNickname.Should().Be(inviter.Nickname);
    }

    [Fact]
    public async Task Success_clears_acceptance_for_incoming_friendly()
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
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelPendingDuelsHandler(ctx);

        var res = await handler.Handle(new CancelPendingDuelsCommand
        {
            UserId = invitee.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().ContainSingle(p => p.User1.Id == inviter.Id && !p.IsAccepted);

        var outbox = await ctx.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_clears_acceptance_for_group_duel()
    {
        var ctx = Context;
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "g1");
        ctx.Users.AddRange(user1, user2);
        ctx.Groups.Add(group);
        ctx.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelPendingDuelsHandler(ctx);

        var res = await handler.Handle(new CancelPendingDuelsCommand
        {
            UserId = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<GroupPendingDuel>()
            .Should().ContainSingle(p => p.IsAcceptedByUser1 && !p.IsAcceptedByUser2);
    }

    [Fact]
    public async Task Success_clears_acceptance_for_tournament_duel()
    {
        var ctx = Context;
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var tournament = new SingleEliminationBracketTournament
        {
            Id = 1,
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "g1"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket
        };

        ctx.Users.AddRange(creator, user1, user2);
        ctx.Tournaments.Add(tournament);
        ctx.PendingDuels.Add(new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CancelPendingDuelsHandler(ctx);

        var res = await handler.Handle(new CancelPendingDuelsCommand
        {
            UserId = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<TournamentPendingDuel>()
            .Should().ContainSingle(p => p.IsAcceptedByUser1 && !p.IsAcceptedByUser2);
    }
}
