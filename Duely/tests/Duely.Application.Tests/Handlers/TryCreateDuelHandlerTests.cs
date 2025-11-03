using System.Threading;
using System.Threading.Tasks;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class TryCreateDuelHandlerTests
{
    private static IOptions<DuelOptions> Opts(int minutes = 30)
        => Options.Create(new DuelOptions { MaxDurationMinutes = minutes });

    private static IDuelManager MakeDuelManagerWithPair(int u1, int u2)
    {
        var m = new DuelManager();
        m.AddUser(u1);
        m.AddUser(u2);
        return m;
    }

    [Fact]
    public async Task Returns_Ok_when_no_pair()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        IDuelManager duelManager = new DuelManager();
        ITaskiClient taski = new TaskiClientSuccessFake(777);
        var msg = new Mock<IMessageSender>(MockBehavior.Strict);
        var handler = new TryCreateDuelHandler(duelManager, taski, msg.Object, Opts(), ctx);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        msg.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Creates_duel_and_sends_messages_when_pair_and_task_ok()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        IDuelManager duelManager = MakeDuelManagerWithPair(u1.Id, u2.Id);
        ITaskiClient taski = new TaskiClientSuccessFake(1234);

        var msg = new Mock<IMessageSender>();
        msg.Setup(m => m.SendMessage(u1.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask)
           .Verifiable();
        msg.Setup(m => m.SendMessage(u2.Id, It.IsAny<object>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask)
           .Verifiable();

        var handler = new TryCreateDuelHandler(duelManager, taski, msg.Object, Opts(45), ctx);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.Should().Be(Result.Ok());
        var duel = await ctx.Duels.SingleAsync();
        duel.Status.Should().Be(DuelStatus.InProgress);
        duel.TaskId.Should().Be(1234);
        duel.User1.Id.Should().Be(u1.Id);
        duel.User2.Id.Should().Be(u2.Id);
        duel.DeadlineTime.Should().BeAfter(duel.StartTime).And.BeCloseTo(duel.StartTime.AddMinutes(45), TimeSpan.FromSeconds(2));

        msg.VerifyAll();
    }

    [Fact]
    public async Task Fails_when_first_user_not_found()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        // Только второй существует
        var u2 = new User { Id = 2 };
        ctx.Users.Add(u2);
        await ctx.SaveChangesAsync();

        IDuelManager duelManager = MakeDuelManagerWithPair(1, 2);
        ITaskiClient taski = new TaskiClientSuccessFake(1);
        var msg = new Mock<IMessageSender>(MockBehavior.Strict);

        var handler = new TryCreateDuelHandler(duelManager, taski, msg.Object, Opts(), ctx);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        msg.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Fails_when_taski_fails()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext();
        await using var _ = conn;

        var u1 = new User { Id = 1 };
        var u2 = new User { Id = 2 };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        IDuelManager duelManager = MakeDuelManagerWithPair(u1.Id, u2.Id);
        ITaskiClient taski = new TaskiClientFailFake();
        var msg = new Mock<IMessageSender>(MockBehavior.Strict);

        var handler = new TryCreateDuelHandler(duelManager, taski, msg.Object, Opts(), ctx);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        msg.VerifyNoOtherCalls();
    }
}
