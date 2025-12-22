using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq;
using System.Text.Json;
using Duely.Application.UseCases.Payloads;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

public class TryCreateDuelHandlerTests : ContextBasedTest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static SendMessagePayload ReadSendPayload(OutboxMessage m)
        => JsonSerializer.Deserialize<SendMessagePayload>(m.Payload, Json)!;

    [Fact]
    public async Task Does_nothing_when_no_pair()
    {
        var ctx = Context;

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns(((int, int)?)null);

        var taskService = new Mock<ITaskService>();

        var taski = new TaskiClientSuccessFake();
        var sender = new Mock<IMessageSender>(MockBehavior.Strict);
        var options = Options.Create(new DuelOptions { MaxDurationMinutes = 30 });

        var handler = new TryCreateDuelHandler(duelManager.Object, taski, options, taskService.Object, ctx, NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        (await ctx.Outbox.AsNoTracking().ToListAsync())
        .Should().BeEmpty();
    }

    [Fact]
    public async Task Creates_duel_and_sends_messages_when_pair_exists()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2); await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns((1, 2));

        var taski = new TaskiClientSuccessFake(["TASK-42"]);
        
        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.ChooseTask(
                It.IsAny<User>(),
                It.IsAny<User>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>()))
            .Returns(new DuelTask("TASK-42", 1));

        var sender = new Mock<IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var options = Options.Create(new DuelOptions { MaxDurationMinutes = 30 });

        var handler = new TryCreateDuelHandler(duelManager.Object, taski, options, taskService.Object, ctx, NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.Include(d => d.User1).Include(d => d.User2).SingleAsync();
        duel.TaskId.Should().Be("TASK-42");
        duel.Status.Should().Be(DuelStatus.InProgress);
        duel.User1!.Id.Should().Be(1);
        duel.User2!.Id.Should().Be(2);

        var messages = await ctx.Outbox.AsNoTracking()
        .Where(m => m.Type == OutboxType.SendMessage)
        .ToListAsync();

        messages.Should().HaveCount(2);

        foreach (var m in messages)
        {
            m.Status.Should().Be(OutboxStatus.ToDo);
            m.RetryUntil.Should().Be(duel.DeadlineTime.AddMinutes(5));

            var p = ReadSendPayload(m);
            p.Type.Should().Be(MessageType.DuelStarted);
            p.DuelId.Should().Be(duel.Id);
            (p.UserId == u1.Id || p.UserId == u2.Id).Should().BeTrue();
        }
    }
    
    [Fact]
    public async Task Creates_duel_with_random_task_when_no_chosen_task()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2); await ctx.SaveChangesAsync();
        
        var d1 = EntityFactory.MakeDuel(1, u1, u2, "SOLVED_TASK_1");
        var d2 = EntityFactory.MakeDuel(2, u2, u1, "SOLVED_TASK_2");
        ctx.Duels.AddRange(d1, d2); await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns((1, 2));

        var taski = new TaskiClientSuccessFake(["SOLVED_TASK_1", "SOLVED_TASK_2"], randomTask: "RANDOM_TASK");
        
        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.ChooseTask(
                It.IsAny<User>(),
                It.IsAny<User>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>()))
            .Returns((DuelTask)null!);

        var sender = new Mock<Duely.Infrastructure.Gateway.Client.Abstracts.IMessageSender>();
        sender.Setup(s => s.SendMessage(1, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        sender.Setup(s => s.SendMessage(2, It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var options = Options.Create(new DuelOptions { MaxDurationMinutes = 30 });

        var handler = new TryCreateDuelHandler(duelManager.Object, taski, options, taskService.Object, ctx, NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        var duel = await ctx.Duels
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Where(d => d.Id != d1.Id && d.Id != d2.Id)
            .SingleAsync();
        duel.TaskId.Should().Be("RANDOM_TASK");
        duel.Status.Should().Be(DuelStatus.InProgress);
        duel.User1!.Id.Should().Be(1);
        duel.User2!.Id.Should().Be(2);

        var messages = await ctx.Outbox.AsNoTracking()
        .Where(m => m.Type == OutboxType.SendMessage)
        .ToListAsync();

        messages.Should().HaveCount(2);

        foreach (var m in messages)
        {
            m.RetryUntil.Should().Be(duel.DeadlineTime.AddMinutes(5));

            var p = ReadSendPayload(m);
            p.Type.Should().Be(MessageType.DuelStarted);
            p.DuelId.Should().Be(duel.Id);
        }
    }
}
