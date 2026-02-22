using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class ChangeRoleHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creator_can_change_member_role()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var member = EntityFactory.MakeUser(2, "member");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = member,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });

        Context.Users.AddRange(creator, member);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new ChangeRoleHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new ChangeRoleCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            TargetUserId = member.Id,
            Role = GroupRole.Manager
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Groups.AsNoTracking()
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .SingleAsync(g => g.Id == group.Id);
        stored.Users.Single(m => m.User.Id == member.Id).Role.Should().Be(GroupRole.Manager);
    }

    [Fact]
    public async Task Manager_cannot_change_creator_role()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var manager = EntityFactory.MakeUser(2, "manager");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = manager,
            Group = group,
            Role = GroupRole.Manager,
            InvitationPending = false
        });

        Context.Users.AddRange(creator, manager);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new ChangeRoleHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new ChangeRoleCommand
        {
            UserId = manager.Id,
            GroupId = group.Id,
            TargetUserId = creator.Id,
            Role = GroupRole.Member
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Member_cannot_change_roles()
    {
        var member = EntityFactory.MakeUser(1, "member");
        var target = EntityFactory.MakeUser(2, "target");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = member,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = target,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });

        Context.Users.AddRange(member, target);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new ChangeRoleHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new ChangeRoleCommand
        {
            UserId = member.Id,
            GroupId = group.Id,
            TargetUserId = target.Id,
            Role = GroupRole.Manager
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
