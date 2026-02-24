using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using FluentAssertions;

namespace Duely.Application.Tests.Handlers;

public sealed class GetUserByNicknameHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_user_when_exists()
    {
        var ctx = Context;
        ctx.Users.Add(EntityFactory.MakeUser(7, "alice"));
        await ctx.SaveChangesAsync();

        var handler = new GetByNicknameHandler(ctx);
        var res = await handler.Handle(new GetUserByNicknameQuery { Nickname = "alice" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(7);
        res.Value.Nickname.Should().Be("alice");
    }

    [Fact]
    public async Task NotFound_when_user_missing()
    {
        var handler = new GetByNicknameHandler(Context);

        var res = await handler.Handle(new GetUserByNicknameQuery { Nickname = "ghost" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
