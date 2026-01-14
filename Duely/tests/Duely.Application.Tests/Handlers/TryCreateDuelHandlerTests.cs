using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Duely.Application.Tests.Handlers;

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
        duelManager.Setup(m => m.TryGetPair()).Returns((DuelPair?)null);

        var taskService = new Mock<ITaskService>();
        var ratingManager = new Mock<IRatingManager>();

        var taski = new TaskiClientSuccessFake();
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager.Object,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
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
        duelManager.Setup(m => m.TryGetPair()).Returns(new DuelPair(1, 2, null));

        var taski = new TaskiClientSuccessFake(["TASK-42"]);
        
        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<User>(),
                It.IsAny<User>(),
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((User _, User _, DuelConfiguration _, IReadOnlyCollection<DuelTask> _, out Dictionary<char, DuelTask> chosen) =>
            {
                chosen = new Dictionary<char, DuelTask>
                {
                    ['A'] = new("TASK-42", 1, [])
                };
            }));

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetTaskLevel(It.IsAny<int>())).Returns(1);

        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager.Object,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.Include(d => d.User1).Include(d => d.User2).SingleAsync();
        duel.Tasks.Should().ContainKey('A');
        duel.Tasks['A'].Id.Should().Be("TASK-42");
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
    public async Task Creates_duel_with_configuration_from_queue()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var configuration = new DuelConfiguration
        {
            Id = 99,
            Owner = u1,
            MaxDurationMinutes = 45,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['B'] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        };
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns(new DuelPair(1, 2, configuration.Id));

        var taski = new TaskiClientSuccessFake(["TASK-99"]);

        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<User>(),
                It.IsAny<User>(),
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((User _, User _, DuelConfiguration _, IReadOnlyCollection<DuelTask> _, out Dictionary<char, DuelTask> chosen) =>
            {
                chosen = new Dictionary<char, DuelTask>
                {
                    ['B'] = new("TASK-99", 1, [])
                };
            }));

        var ratingManager = new Mock<IRatingManager>();
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager.Object,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.Include(d => d.Configuration).SingleAsync();
        duel.Configuration.Id.Should().Be(configuration.Id);
        duel.Tasks.Should().ContainKey('B');
    }
    
    [Fact]
    public async Task Fails_when_tasks_not_selected()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2); await ctx.SaveChangesAsync();

        var duelManager = new Mock<IDuelManager>();
        duelManager.Setup(m => m.TryGetPair()).Returns(new DuelPair(1, 2, null));

        var taski = new TaskiClientSuccessFake(["RANDOM_TASK"]);
        
        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<User>(),
                It.IsAny<User>(),
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(false);

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetTaskLevel(It.IsAny<int>())).Returns(1);

        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager.Object,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    private delegate void TryChooseTasksCallback(
        User user1,
        User user2,
        DuelConfiguration configuration,
        IReadOnlyCollection<DuelTask> tasks,
        out Dictionary<char, DuelTask> chosenTasks);
}
