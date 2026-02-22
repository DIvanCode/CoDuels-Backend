using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class GetGroupInvitationsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_only_pending_invitations()
    {
        var user = EntityFactory.MakeUser(1, "user");
        var groupA = EntityFactory.MakeGroup(1, "Alpha");
        var groupB = EntityFactory.MakeGroup(2, "Beta");
        var groupC = EntityFactory.MakeGroup(3, "Gamma");

        groupA.Users.Add(new GroupMembership
        {
            User = user,
            Group = groupA,
            Role = GroupRole.Member,
            InvitationPending = true
        });
        groupB.Users.Add(new GroupMembership
        {
            User = user,
            Group = groupB,
            Role = GroupRole.Manager,
            InvitationPending = true
        });
        groupC.Users.Add(new GroupMembership
        {
            User = user,
            Group = groupC,
            Role = GroupRole.Member,
            InvitationPending = false
        });

        Context.Users.Add(user);
        Context.Groups.AddRange(groupA, groupB, groupC);
        await Context.SaveChangesAsync();

        var handler = new GetGroupInvitationsHandler(Context);
        var res = await handler.Handle(new GetGroupInvitationsQuery
        {
            UserId = user.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().HaveCount(2);
        res.Value.Should().ContainSingle(i => i.GroupId == groupA.Id && i.Role == GroupRole.Member);
        res.Value.Should().ContainSingle(i => i.GroupId == groupB.Id && i.Role == GroupRole.Manager);
    }

    [Fact]
    public async Task Returns_empty_when_user_missing()
    {
        var handler = new GetGroupInvitationsHandler(Context);
        var res = await handler.Handle(new GetGroupInvitationsQuery
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().BeEmpty();
    }
}
