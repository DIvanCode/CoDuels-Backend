using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Duely.Application.Tests.Handlers;

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
        duelManager.Verify(
            m => m.AddUser(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()),
            Times.Never);
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
        duelManager.Verify(
            m => m.AddUser(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()),
            Times.Never);
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
        duelManager.Verify(m => m.AddUser(1, 1500, It.IsAny<DateTime>(), null, null), Times.Once);
    }

    [Fact]
    public async Task NotFound_when_opponent_nickname_missing()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1,
            OpponentNickname = "missing"
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        duelManager.Verify(
            m => m.AddUser(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()),
            Times.Never);
    }

    [Fact]
    public async Task Success_adds_user_with_expected_opponent()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.IsUserWaiting(1)).Returns(false);
        duelManager.Setup(m => m.GetWaitingUsers()).Returns(Array.Empty<WaitingUser>());

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1,
            OpponentNickname = "u2"
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(m => m.AddUser(1, u1.Rating, It.IsAny<DateTime>(), u2.Id, null), Times.Once);
    }

    [Fact]
    public async Task Success_adds_user_with_configuration()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);

        var configuration = new DuelConfiguration
        {
            Id = 10,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentSolution = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        };
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.IsUserWaiting(1)).Returns(false);
        duelManager.Setup(m => m.GetWaitingUsers()).Returns(Array.Empty<WaitingUser>());

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1,
            ConfigurationId = configuration.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(
            m => m.AddUser(1, u1.Rating, It.IsAny<DateTime>(), null, configuration.Id),
            Times.Once);
    }

    [Fact]
    public async Task Success_adds_user_when_invitation_contains_configuration()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.DuelConfigurations.Add(new DuelConfiguration
        {
            Id = 10,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentSolution = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        });
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.IsUserWaiting(u1.Id)).Returns(false);

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = 10
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        duelManager.Verify(
            m => m.AddUser(
                u1.Id,
                u1.Rating,
                It.IsAny<DateTime>(),
                u2.Id,
                10),
            Times.Once);
    }

    [Fact]
    public async Task NotFound_when_configuration_missing()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        ctx.Users.Add(u1);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();

        var handler = new AddUserHandler(ctx, duelManager.Object, NullLogger<AddUserHandler>.Instance);

        var res = await handler.Handle(new AddUserCommand
        {
            UserId = 1,
            ConfigurationId = 999
        }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        duelManager.Verify(
            m => m.AddUser(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<int?>()),
            Times.Never);
    }
}
