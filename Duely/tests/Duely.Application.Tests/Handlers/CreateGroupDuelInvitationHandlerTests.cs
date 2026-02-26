using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels.Invitations;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class CreateGroupDuelInvitationHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
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
    public async Task NotFound_when_group_missing()
    {
        var user = EntityFactory.MakeUser(1, "u1");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
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
    public async Task Forbidden_when_user_cannot_create_duel()
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
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
    public async Task NotFound_when_user1_not_in_group()
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
            User = user2,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task NotFound_when_user2_not_in_group()
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
        Context.Users.AddRange(creator, user1, user2);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task AlreadyExists_when_user1_has_active_duel()
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
        Context.Duels.Add(EntityFactory.MakeDuel(10, user1, creator));
        await Context.SaveChangesAsync();

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task AlreadyExists_when_user2_has_active_duel()
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
        Context.Duels.Add(EntityFactory.MakeDuel(10, user2, creator));
        await Context.SaveChangesAsync();

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task NotFound_when_configuration_missing()
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Creates_pending_and_outbox_messages()
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>().Should().ContainSingle();
        var messages = await Context.OutboxMessages.AsNoTracking().ToListAsync();
        messages.Should().HaveCount(2);
        messages.Should().OnlyContain(m => ((SendMessagePayload)m.Payload).Message is GroupDuelInvitationMessage);
    }

    [Fact]
    public async Task Does_not_create_duplicate_when_pending_exists_in_pending_duels()
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

        var handler = new CreateGroupDuelInvitationHandler(Context, new GroupPermissionsService());

        var res = await handler.Handle(new CreateGroupDuelInvitationCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            User1Id = user1.Id,
            User2Id = user2.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Context.PendingDuels.OfType<GroupPendingDuel>().Should().ContainSingle();
        var outbox = await Context.OutboxMessages.AsNoTracking().ToListAsync();
        outbox.Should().BeEmpty();
    }
}
