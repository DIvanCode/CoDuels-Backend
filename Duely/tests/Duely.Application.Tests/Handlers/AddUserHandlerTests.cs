using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

public class AddUserHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;
        var duelManager = new Mock<IDuelManager>();

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        duelManager.Verify(m => m.IsUserWaiting(It.IsAny<int>()), Times.Never);
        duelManager.Verify(m => m.AddUser(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task AlreadyExists_when_user_already_waiting()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.IsUserWaiting(1)).Returns(true);

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
        duelManager.Verify(m => m.AddUser(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Success_adds_user_to_waiting_pool()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        u1.Rating = 1500;
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.IsUserWaiting(1)).Returns(false);

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(m => m.AddUser(1, 1500, It.IsAny<DateTime>()), Times.Once);
    }
}

