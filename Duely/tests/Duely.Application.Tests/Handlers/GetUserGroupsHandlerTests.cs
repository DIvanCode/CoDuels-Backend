using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Services.Groups;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class GetUserGroupsHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_groups_with_read_permission()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        var groupA = EntityFactory.MakeGroup(1, "Alpha");
        var groupB = EntityFactory.MakeGroup(2, "Beta");
        var groupC = EntityFactory.MakeGroup(3, "Gamma");

        Context.Users.Add(user);
        groupA.Users.Add(EntityFactory.MakeGroupMembership(user, groupA, GroupRole.Member));
        groupB.Users.Add(EntityFactory.MakeGroupMembership(user, groupB, GroupRole.Manager));
        groupC.Users.Add(EntityFactory.MakeGroupMembership(user, groupC, GroupRole.Creator));
        Context.Groups.AddRange(groupA, groupB, groupC);
        await Context.SaveChangesAsync();

        var handler = new GetUserGroupsHandler(Context, new GroupPermissionsService());
        var res = await handler.Handle(new GetUserGroupsQuery
        {
            UserId = user.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Select(g => g.Name).Should().BeEquivalentTo(["Alpha", "Beta", "Gamma"]);
    }
}
