using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public class GetHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_user_when_exists()
    {
        var ctx = Context;
        ctx.Users.Add(EntityFactory.MakeUser(7, "alice"));
        await ctx.SaveChangesAsync();

        var handler = new GetHandler(ctx);
        var res = await handler.Handle(new GetUserQuery { UserId = 7 }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Nickname.Should().Be("alice");
    }

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new GetHandler(Context);

        var res = await handler.Handle(new GetUserQuery { UserId = 999 }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}