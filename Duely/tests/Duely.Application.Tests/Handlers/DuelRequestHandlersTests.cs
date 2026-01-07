using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public class DuelRequestHandlersTests : ContextBasedTest
{
    [Fact]
    public async Task Create_request_creates_pending_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var configuration = MakeConfiguration(10);
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var handler = new CreateDuelRequestHandler(ctx);
        var res = await handler.Handle(new CreateDuelRequestCommand
        {
            UserId = u1.Id,
            OpponentNickname = u2.Nickname,
            ConfigurationId = configuration.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var duel = await ctx.Duels.AsNoTracking().Include(d => d.User1).Include(d => d.User2).SingleAsync();
        duel.Status.Should().Be(DuelStatus.Pending);
        duel.Tasks.Should().BeEmpty();
        duel.User1.Id.Should().Be(u1.Id);
        duel.User2.Id.Should().Be(u2.Id);
    }

    [Fact]
    public async Task Accept_request_starts_duel_and_sends_messages()
    {
        var ctx = Context;
        var now = DateTime.UtcNow;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var configuration = MakeConfiguration(20, level: 2, topics: ["dp", "graphs"]);
        ctx.DuelConfigurations.Add(configuration);

        var duel = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = now.AddMinutes(-10),
            DeadlineTime = now.AddMinutes(20),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var taskiClient = new TaskiClientFake([
            new TaskResponse { Id = "task-1", Level = 1, Topics = ["dp"] },
            new TaskResponse { Id = "task-2", Level = 2, Topics = ["dp", "graphs"] }
        ]);

        var handler = new AcceptDuelRequestHandler(ctx, taskiClient, new TaskService());
        var res = await handler.Handle(new AcceptDuelRequestCommand
        {
            UserId = u2.Id,
            DuelId = duel.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var updated = await ctx.Duels.AsNoTracking().SingleAsync(d => d.Id == duel.Id);
        updated.Status.Should().Be(DuelStatus.InProgress);
        updated.Tasks.Should().ContainKey('A');
        updated.Tasks['A'].Id.Should().Be("task-2");

        var outbox = await ctx.Outbox.AsNoTracking().ToListAsync();
        outbox.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deny_request_removes_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var configuration = MakeConfiguration(30);
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var duel = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var handler = new DenyDuelRequestHandler(ctx);
        var res = await handler.Handle(new DenyDuelRequestCommand
        {
            UserId = u2.Id,
            DuelId = duel.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_request_removes_duel()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);

        var configuration = MakeConfiguration(40);
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var duel = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };
        ctx.Duels.Add(duel);
        await ctx.SaveChangesAsync();

        var handler = new CancelDuelRequestHandler(ctx);
        var res = await handler.Handle(new CancelDuelRequestCommand
        {
            UserId = u1.Id,
            DuelId = duel.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Get_pending_requests_returns_incoming_and_outgoing()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var u3 = EntityFactory.MakeUser(3, "u3");
        ctx.Users.AddRange(u1, u2, u3);

        var configuration = MakeConfiguration(50);
        ctx.DuelConfigurations.Add(configuration);
        await ctx.SaveChangesAsync();

        var outgoing = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };

        var incoming = new Duel
        {
            Status = DuelStatus.Pending,
            Configuration = configuration,
            Tasks = new Dictionary<char, DuelTask>(),
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u3,
            User1InitRating = 1500,
            User2 = u1,
            User2InitRating = 1500
        };

        ctx.Duels.AddRange(outgoing, incoming);
        await ctx.SaveChangesAsync();

        var handler = new GetPendingDuelRequestsHandler(ctx);
        var res = await handler.Handle(new GetPendingDuelRequestsQuery
        {
            UserId = u1.Id
        }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Outgoing.Should().ContainSingle();
        res.Value.Outgoing[0].OpponentNickname.Should().Be(u2.Nickname);
        res.Value.Incoming.Should().ContainSingle();
        res.Value.Incoming[0].OpponentNickname.Should().Be(u3.Nickname);
    }

    private static DuelConfiguration MakeConfiguration(
        int id,
        int level = 1,
        string[]? topics = null)
    {
        return new DuelConfiguration
        {
            Id = id,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = level,
                    Topics = topics ?? []
                }
            }
        };
    }

    private sealed class TaskiClientFake : ITaskiClient
    {
        private readonly List<TaskResponse> _tasks;

        public TaskiClientFake(List<TaskResponse> tasks)
        {
            _tasks = tasks;
        }

        public Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok(new TaskListResponse { Tasks = _tasks }));

        public Task<Result> TestSolutionAsync(
            string taskId,
            string solutionId,
            string solution,
            string language,
            CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok());
    }
}
