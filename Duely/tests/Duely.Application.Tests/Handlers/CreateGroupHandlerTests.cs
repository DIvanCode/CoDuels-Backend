using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Groups;
using Duely.Domain.Models;
using Duely.Domain.Models.Groups;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public sealed class CreateGroupHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Creates_group_and_membership()
    {
        var user = EntityFactory.MakeUser(1, "owner");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var handler = new CreateGroupHandler(Context);
        var res = await handler.Handle(new CreateGroupCommand
        {
            UserId = user.Id,
            Name = "Alpha"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Name.Should().Be("Alpha");

        var group = await Context.Groups.AsNoTracking()
            .SingleAsync(g => g.Id == res.Value.Id);
        group.Name.Should().Be("Alpha");

        var userWithGroups = await Context.Users.AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Include(u => u.Groups)
                .ThenInclude(m => m.Group)
            .SingleAsync();
        userWithGroups.Groups.Should().ContainSingle(m => m.Group.Id == group.Id);
        userWithGroups.Groups.Single(m => m.Group.Id == group.Id).Role.Should().Be(GroupRole.Creator);
    }

    [Fact]
    public async Task Returns_not_found_when_user_missing()
    {
        var handler = new CreateGroupHandler(Context);
        var res = await handler.Handle(new CreateGroupCommand
        {
            UserId = 999,
            Name = "Alpha"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
