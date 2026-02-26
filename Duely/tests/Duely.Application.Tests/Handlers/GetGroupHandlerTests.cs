using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class GetGroupHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_group_when_user_has_read_permission()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var userGroupRole = EntityFactory.MakeGroupMembership(user, group, GroupRole.Member);
        group.Users.Add(userGroupRole);
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new GetGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetGroupQuery
        {
            UserId = user.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(group.Id);
        res.Value.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task Forbidden_when_user_lacks_read_permission()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new GetGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetGroupQuery
        {
            UserId = user.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_not_found_when_group_missing()
    {
        var handler = new GetGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetGroupQuery
        {
            UserId = 1,
            GroupId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
