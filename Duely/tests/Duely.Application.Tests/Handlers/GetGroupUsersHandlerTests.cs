using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class GetGroupUsersHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_users_with_pending_status()
    {
        var viewer = EntityFactory.MakeUser(1, "viewer");
        var member = EntityFactory.MakeUser(2, "member");
        var invited = EntityFactory.MakeUser(3, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = viewer,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = member,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true,
            InvitedBy = viewer
        });

        Context.Users.AddRange(viewer, member, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new GetGroupUsersHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetGroupUsersQuery
        {
            UserId = viewer.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().HaveCount(3);
        res.Value.Should().ContainSingle(u =>
            u.User.Id == invited.Id &&
            u.Status == GroupUserStatus.Pending &&
            u.InvitedBy != null &&
            u.InvitedBy.Id == viewer.Id &&
            u.InvitedBy.Nickname == viewer.Nickname);
        res.Value.Should().ContainSingle(u => u.User.Id == member.Id && u.Status == GroupUserStatus.Active);
    }

    [Fact]
    public async Task Forbidden_when_user_is_invited()
    {
        var invited = EntityFactory.MakeUser(1, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true
        });

        Context.Users.Add(invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new GetGroupUsersHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetGroupUsersQuery
        {
            UserId = invited.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
