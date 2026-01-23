using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

public class RemoveUserHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;
        var duelManager = new Mock<IDuelManager>();

        var handler = new RemoveUserHandler(ctx, duelManager.Object, NullLogger<RemoveUserHandler>.Instance);

        var res = await handler.Handle(new RemoveUserCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        duelManager.Verify(m => m.RemoveUser(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Success_when_user_not_waiting()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();

        var handler = new RemoveUserHandler(ctx, duelManager.Object, NullLogger<RemoveUserHandler>.Instance);

        var res = await handler.Handle(new RemoveUserCommand
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(m => m.RemoveUser(1), Times.Once);
    }

    [Fact]
    public async Task Success_removes_user_from_waiting_pool()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();

        var handler = new RemoveUserHandler(ctx, duelManager.Object, NullLogger<RemoveUserHandler>.Instance);

        var res = await handler.Handle(new RemoveUserCommand
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(m => m.RemoveUser(1), Times.Once);
    }
}

