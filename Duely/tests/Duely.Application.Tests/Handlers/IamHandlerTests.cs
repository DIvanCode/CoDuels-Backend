using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using FluentAssertions;
using Xunit;

public class IamHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_current_user()
    {
        var ctx = Context;
        ctx.Users.Add(EntityFactory.MakeUser(7, "alice"));
        await ctx.SaveChangesAsync();

        var handler = new IamHandler(ctx);
        var res = await handler.Handle(new IamQuery { UserId = 7 }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Nickname.Should().Be("alice");
    }
}
