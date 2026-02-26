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

public sealed class LeaveGroupHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Removes_membership()
    {
        var user = EntityFactory.MakeUser(1, "member");
        var group = EntityFactory.MakeGroup(1, "Alpha");
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

        var handler = new LeaveGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new LeaveGroupCommand
        {
            UserId = user.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Groups.AsNoTracking()
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .SingleAsync(g => g.Id == group.Id);
        stored.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_forbidden_when_not_member()
    {
        var user = EntityFactory.MakeUser(1, "member");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new LeaveGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new LeaveGroupCommand
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
        var handler = new LeaveGroupHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new LeaveGroupCommand
        {
            UserId = 1,
            GroupId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

}
