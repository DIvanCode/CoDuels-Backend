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

public sealed class ExcludeUserHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creator_can_exclude_manager()
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

        var handler = new ExcludeUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new ExcludeUserCommand
        {
            UserId = creator.Id,
            GroupId = group.Id,
            TargetUserId = manager.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Groups.AsNoTracking()
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .SingleAsync(g => g.Id == group.Id);
        stored.Users.Should().ContainSingle(m => m.User.Id == creator.Id);
    }

    [Fact]
    public async Task Manager_cannot_exclude_manager()
    {
        var manager = EntityFactory.MakeUser(1, "manager");
        var otherManager = EntityFactory.MakeUser(2, "manager2");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = manager,
            Group = group,
            Role = GroupRole.Manager,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = otherManager,
            Group = group,
            Role = GroupRole.Manager,
            InvitationPending = false
        });

        Context.Users.AddRange(manager, otherManager);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new ExcludeUserHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new ExcludeUserCommand
        {
            UserId = manager.Id,
            GroupId = group.Id,
            TargetUserId = otherManager.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
