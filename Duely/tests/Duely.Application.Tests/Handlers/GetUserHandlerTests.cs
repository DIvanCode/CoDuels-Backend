using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GetUserHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_UserDto_when_exists()
    {
        var user = EntityFactory.MakeUser(1, "neo");
        Context.Users.Add(user); await Context.SaveChangesAsync();

        var handler = new GetHandler(Context);
        var res = await handler.Handle(new GetUserQuery { UserId = 1 }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Id.Should().Be(1);
        res.Value.Nickname.Should().Be("neo");
    }

    [Fact]
    public async Task Returns_NotFound_when_absent()
    {
        var handler = new GetHandler(Context);

        var res = await handler.Handle(new GetUserQuery { UserId = 777 }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }
}
