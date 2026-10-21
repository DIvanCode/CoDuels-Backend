using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class AcceptGroupDuelInvitationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = 1,
            GroupId = 1,
            OpponentNickname = "u2"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Accepts_when_group_pending_in_pending_duels()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Users.AddRange(user1, user2);
        Context.Groups.Add(group);
        Context.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = group.Id,
            OpponentNickname = user2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>()
            .Should().ContainSingle(d => d.IsAcceptedByUser1);
    }

    [Fact]
    public async Task Accepts_when_user2_accepts()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Users.AddRange(user1, user2);
        Context.Groups.Add(group);
        Context.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user2.Id,
            GroupId = group.Id,
            OpponentNickname = user1.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>()
            .Should().ContainSingle(d => d.IsAcceptedByUser2);
    }

    [Fact]
    public async Task Can_accept_group_invitation_while_user_has_active_duel()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(user1, user2);
        Context.Duels.Add(EntityFactory.MakeDuel(10, user1, user2));
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Groups.Add(group);
        Context.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = 1,
            OpponentNickname = user2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>().Should().ContainSingle(d => d.IsAcceptedByUser1);
    }

    [Fact]
    public async Task NotFound_when_group_pending_missing()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(user1, user2);
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = 1,
            OpponentNickname = user2.Nickname
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Accepts_pair_when_request_configuration_differs()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Users.AddRange(user1, user2);
        Context.Groups.Add(group);
        Context.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = user1,
            User1 = user1,
            User2 = user2,
            Configuration = new Duely.Domain.Models.Duels.DuelConfiguration { Id = 10, IsRated = false, MaxDurationMinutes = 30, TasksCount = 1, TasksOrder = Duely.Domain.Models.Duels.DuelTasksOrder.Sequential, TasksConfigurations = [] },
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = group.Id,
            OpponentNickname = user2.Nickname,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>()
            .Should().ContainSingle(p => p.IsAcceptedByUser1 && p.Configuration!.Id == 10);
    }

    [Fact]
    public async Task Accepting_preserves_ranked_pending_and_sends_no_message()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Users.AddRange(user1, user2);
        Context.Groups.Add(group);
        Context.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user1,
                Rating = user1.Rating,
                CreatedAt = DateTime.UtcNow
            },
            new GroupPendingDuel
            {
                Type = PendingDuelType.Group,
                Group = group,
                CreatedBy = user1,
                User1 = user1,
                User2 = user2,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = group.Id,
            OpponentNickname = user2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<RankedPendingDuel>().Should().ContainSingle();

        var outboxMessages = await Context.OutboxMessages.AsNoTracking().ToListAsync();
        outboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Accepting_preserves_outgoing_friendly_invitation()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var other = EntityFactory.MakeUser(3, "u3");
        var group = EntityFactory.MakeGroup(1, "g");
        Context.Users.AddRange(user1, user2, other);
        Context.Groups.Add(group);
        Context.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = user1,
                User2 = other,
                Configuration = null,
                IsAccepted = false,
                CreatedAt = DateTime.UtcNow
            },
            new GroupPendingDuel
            {
                Type = PendingDuelType.Group,
                Group = group,
                CreatedBy = user1,
                User1 = user1,
                User2 = user2,
                Configuration = null,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var handler = new AcceptGroupDuelInvitationHandler(Context);

        var res = await handler.Handle(new AcceptGroupDuelInvitationCommand
        {
            UserId = user1.Id,
            GroupId = group.Id,
            OpponentNickname = user2.Nickname
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Should().ContainSingle();

        (await Context.OutboxMessages.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }
}
