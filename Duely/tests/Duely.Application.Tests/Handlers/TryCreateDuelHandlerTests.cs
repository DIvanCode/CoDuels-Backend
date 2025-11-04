using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class TryCreateDuelHandlerTests
{

    [Fact]
    public async Task Does_nothing_when_no_pair()
    {
        var (ctx, conn) = Duely.Application.Tests.TestHelpers.DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns(( (int,int)? )null);

        var taski = new TaskiClientSuccessFake("TASK-1");
        var sender = new Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>(MockBehavior.Strict);
        var options = Options.Create(new DuelOptions { MaxDurationMinutes = 30 });

        var handler = new TryCreateDuelHandler(duelManager.Object, taski, sender.Object, options, ctx);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Creates_duel_and_sends_messages_when_pair_exists()
    {
        var (ctx, conn) = Duely.Application.Tests.TestHelpers.DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2); await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns((1, 2));

        var taski = new TaskiClientSuccessFake("TASK-42");

        var sender = new Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var options = Options.Create(new DuelOptions { MaxDurationMinutes = 30 });

        var handler = new TryCreateDuelHandler(duelManager.Object, taski, sender.Object, options, ctx);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.Include(d => d.User1).Include(d => d.User2).SingleAsync();
        duel.TaskId.Should().Be("TASK-42");
        duel.Status.Should().Be(DuelStatus.InProgress);
        duel.User1!.Id.Should().Be(1);
        duel.User2!.Id.Should().Be(2);

        sender.VerifyAll();
    }
}
