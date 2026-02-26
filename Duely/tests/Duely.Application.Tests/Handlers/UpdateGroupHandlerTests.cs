using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class UpdateGroupHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Updates_group_when_user_has_permission()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var userGroup = EntityFactory.MakeGroupMembership(user, group, GroupRole.Manager);
        group.Users.Add(userGroup);
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new UpdateGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new UpdateGroupCommand
        {
            UserId = user.Id,
            GroupId = group.Id,
            Name = "Beta"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Name.Should().Be("Beta");

        var entity = await Context.Groups.AsNoTracking()
            .SingleAsync(g => g.Id == group.Id);
        entity.Name.Should().Be("Beta");
    }

    [Fact]
    public async Task Forbidden_when_user_lacks_edit_permission()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var userGroup = EntityFactory.MakeGroupMembership(user, group, GroupRole.Member);
        group.Users.Add(userGroup);
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new UpdateGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new UpdateGroupCommand
        {
            UserId = user.Id,
            GroupId = group.Id,
            Name = "Beta"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_not_found_when_group_missing()
    {
        var handler = new UpdateGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new UpdateGroupCommand
        {
            UserId = 1,
            GroupId = 999,
            Name = "Beta"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
