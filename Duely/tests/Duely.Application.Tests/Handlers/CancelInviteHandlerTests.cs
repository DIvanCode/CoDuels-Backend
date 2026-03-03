using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class CancelInviteHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Inviter_can_cancel_invitation()
    {
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invited = EntityFactory.MakeUser(2, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = inviter,
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
            InvitedBy = inviter
        });

        Context.Users.AddRange(inviter, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelInviteHandler(Context);
        var res = await handler.Handle(new CancelInviteCommand
        {
            UserId = inviter.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var stored = await Context.Groups.AsNoTracking()
            .Include(g => g.Users)
            .ThenInclude(m => m.User)
            .SingleAsync(g => g.Id == group.Id);
        stored.Users.Should().ContainSingle(m => m.User.Id == inviter.Id);
    }

    [Fact]
    public async Task Returns_forbidden_when_not_inviter()
    {
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var other = EntityFactory.MakeUser(2, "other");
        var invited = EntityFactory.MakeUser(3, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = inviter,
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
            InvitedBy = inviter
        });

        Context.Users.AddRange(inviter, other, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelInviteHandler(Context);
        var res = await handler.Handle(new CancelInviteCommand
        {
            UserId = other.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }

    [Fact]
    public async Task Returns_forbidden_when_invitation_already_accepted()
    {
        var inviter = EntityFactory.MakeUser(1, "inviter");
        var invited = EntityFactory.MakeUser(2, "invited");
        var group = EntityFactory.MakeGroup(1, "Alpha");
        group.Users.Add(new GroupMembership
        {
            User = inviter,
            Group = group,
            Role = GroupRole.Creator,
            InvitationPending = false
        });
        group.Users.Add(new GroupMembership
        {
            User = invited,
            Group = group,
            Role = GroupRole.Member,
            InvitationPending = false,
            InvitedBy = inviter
        });

        Context.Users.AddRange(inviter, invited);
        Context.Groups.Add(group);
        await Context.SaveChangesAsync();

        var handler = new CancelInviteHandler(Context);
        var res = await handler.Handle(new CancelInviteCommand
        {
            UserId = inviter.Id,
            GroupId = group.Id,
            InvitedUserId = invited.Id
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is ForbiddenError);
    }
}
