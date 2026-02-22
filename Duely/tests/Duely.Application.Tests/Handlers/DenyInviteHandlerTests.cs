using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class DenyInviteHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Removes_pending_membership()
    {
        var user = EntityFactory.MakeUser(1, "user");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = user,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = true
        });

        Context.Users.Add(user);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new DenyInviteHandler(Context);
        var res = await handler.Handle(new DenyInviteCommand
        {
            UserId = user.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Users.AsNoTracking()
            .Include(u => u.Groups)
            .ThenInclude(m => m.Group)
            .SingleAsync(u => u.Id == user.Id);
        stored.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_not_found_when_invitation_missing()
    {
        var user = EntityFactory.MakeUser(1, "user");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new DenyInviteHandler(Context);
        var res = await handler.Handle(new DenyInviteCommand
        {
            UserId = user.Id,
            GroupId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task Returns_forbidden_when_membership_not_pending()
    {
        var user = EntityFactory.MakeUser(1, "user");
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

        var handler = new DenyInviteHandler(Context);
        var res = await handler.Handle(new DenyInviteCommand
        {
            UserId = user.Id,
            GroupId = group.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
