using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public sealed class GroupPermissionsServiceTests
{
    private static GroupMembership MakeMembership(GroupRole role, bool pending)
    {
        return new GroupMembership
        {
            User = new User
            {
                Id = 1,
                Nickname = "user",
                PasswordSalt = "salt",
                PasswordHash = "hash",
                Rating = 1500,
                CreatedAt = DateTime.UtcNow
            },
            Group = new Group
            {
                Id = 1,
                Name = "group"
            },
            Role = role,
            InvitationPending = pending
        };
    }

    [Theory]
    [InlineData(GroupRole.Creator, false, true)]
    [InlineData(GroupRole.Manager, false, true)]
    [InlineData(GroupRole.Member, false, true)]
    [InlineData(GroupRole.Creator, true, false)]
    [InlineData(GroupRole.Manager, true, false)]
    [InlineData(GroupRole.Member, true, false)]
    public void CanViewGroup_respects_pending_and_role(
        GroupRole role,
        bool pending,
        bool expected)
    {
        var service = new GroupPermissionsService();
        var membership = MakeMembership(role, pending);

        service.CanViewGroup(membership).Should().Be(expected);
    }

    [Theory]
    [InlineData(GroupRole.Creator, false, true)]
    [InlineData(GroupRole.Manager, false, true)]
    [InlineData(GroupRole.Member, false, false)]
    [InlineData(GroupRole.Creator, true, false)]
    [InlineData(GroupRole.Manager, true, false)]
    [InlineData(GroupRole.Member, true, false)]
    public void CanUpdateGroup_respects_pending_and_role(
        GroupRole role,
        bool pending,
        bool expected)
    {
        var service = new GroupPermissionsService();
        var membership = MakeMembership(role, pending);

        service.CanUpdateGroup(membership).Should().Be(expected);
    }

    [Theory]
    [InlineData(GroupRole.Creator, false, GroupRole.Manager, true)]
    [InlineData(GroupRole.Creator, false, GroupRole.Member, true)]
    [InlineData(GroupRole.Manager, false, GroupRole.Manager, true)]
    [InlineData(GroupRole.Manager, false, GroupRole.Member, true)]
    [InlineData(GroupRole.Member, false, GroupRole.Member, false)]
    [InlineData(GroupRole.Creator, true, GroupRole.Member, false)]
    [InlineData(GroupRole.Manager, true, GroupRole.Member, false)]
    [InlineData(GroupRole.Member, true, GroupRole.Member, false)]
    public void CanAssignRole_respects_rbac_and_pending(
        GroupRole actorRole,
        bool pending,
        GroupRole targetRole,
        bool expected)
    {
        var service = new GroupPermissionsService();
        var actor = MakeMembership(actorRole, pending);

        service.CanAssignRole(actor, targetRole).Should().Be(expected);
    }

    [Theory]
    [InlineData(GroupRole.Creator, false, GroupRole.Manager, true)]
    [InlineData(GroupRole.Creator, false, GroupRole.Member, true)]
    [InlineData(GroupRole.Manager, false, GroupRole.Member, true)]
    [InlineData(GroupRole.Manager, false, GroupRole.Manager, false)]
    [InlineData(GroupRole.Member, false, GroupRole.Member, false)]
    [InlineData(GroupRole.Creator, true, GroupRole.Member, false)]
    [InlineData(GroupRole.Manager, true, GroupRole.Member, false)]
    [InlineData(GroupRole.Member, true, GroupRole.Member, false)]
    public void CanExclude_respects_rbac_and_pending(
        GroupRole actorRole,
        bool pending,
        GroupRole targetRole,
        bool expected)
    {
        var service = new GroupPermissionsService();
        var actor = MakeMembership(actorRole, pending);

        service.CanExclude(actor, targetRole).Should().Be(expected);
    }
}
