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

public class CancelGroupDuelInvitationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = 1,
            GroupId = 1,
            User1Id = 2,
            User2Id = 3
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Forbidden_when_user_cannot_cancel_duel()
    {
        var user = EntityFactory.MakeUser(1, "u1");
        var group = EntityFactory.MakeGroup(1, "g");
        group.Users.Add(new GroupMembership
        {
            User = user,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = user.Id,
            GroupId = group.Id,
            User1Id = 2,
            User2Id = 3
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task NotFound_when_group_missing()
    {
        var user = EntityFactory.MakeUser(1, "u1");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = user.Id,
            GroupId = 999,
            User1Id = 2,
            User2Id = 3
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_user1_missing()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var group = EntityFactory.MakeGroup(1, "g");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        Context.Users.Add(creator);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = 999,
            User2Id = 2
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_user2_missing()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var group = EntityFactory.MakeGroup(1, "g");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = user1,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        Context.Users.AddRange(creator, user1);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Success_when_pending_missing()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = user1,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = user2,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var outbox = await Context.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }

    [Fact]
    public async Task Success_when_pending_exists_in_pending_duels_removes_and_sends()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var group = EntityFactory.MakeGroup(1, "g");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = user1,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = user2,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        Context.PendingDuels.Add(new GroupPendingDuel
        {
            Type = PendingDuelType.Group,
            Group = group,
            CreatedBy = creator,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var handler = new CancelGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CancelGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>().Should().BeEmpty();
        var outbox = await Context.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().HaveCount(2);
        outbox.Should().OnlyContain(m => ((SendMessagePayload)m.Payload).Message is GroupDuelInvitationCanceledMessage);
    }
}
