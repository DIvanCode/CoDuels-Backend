using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class InviteUserHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creates_pending_membership_with_invited_by()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var invited = EntityFactory.MakeUser(2, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });

        Context.Users.AddRange(creator, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new InviteUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new InviteUserCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id,
            Role = GroupRole.Member
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var storedGroup = await Context.Groups.AsNoTracking()
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .Include(g => g.Users)
            .ThenInclude(m => m.InvitedBy)
            .SingleAsync(g => g.Id == group.Id);

        storedGroup.Users.Should().ContainSingle(m => m.User.Id == invited.Id);
        var invitation = storedGroup.Users.Single(m => m.User.Id == invited.Id);
        invitation.InvitationPending.Should().BeTrue();
        invitation.Role.Should().Be(GroupRole.Member);
        invitation.InvitedBy.Should().NotBeNull();
        invitation.InvitedBy!.Id.Should().Be(creator.Id);
    }

    [Fact]
    public async Task Returns_forbidden_when_member_invites()
    {
        var member = EntityFactory.MakeUser(1, "member");
        var invited = EntityFactory.MakeUser(2, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = member,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });

        Context.Users.AddRange(member, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new InviteUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new InviteUserCommand
        {
            UserId = member.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id,
            Role = GroupRole.Member
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_conflict_when_invitation_exists()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var invited = EntityFactory.MakeUser(2, "invited");
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
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true,
            InvitedBy = creator
        });

        Context.Users.AddRange(creator, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new InviteUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new InviteUserCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id,
            Role = GroupRole.Member
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task Returns_forbidden_for_creator_role_invite()
    {
        var creator = EntityFactory.MakeUser(1, "creator");
        var invited = EntityFactory.MakeUser(2, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = creator,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });

        Context.Users.AddRange(creator, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new InviteUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new InviteUserCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id,
            Role = GroupRole.Creator
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
