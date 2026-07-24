using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class TryCreateDuelHandlerTests : ContextBasedTest
{
    private static SendMessagePayload ReadSendPayload(OutboxMessage m)
        => (SendMessagePayload)m.Payload;

    private static ITournamentMatchmakingStrategyResolver CreateTournamentStrategyResolver()
    {
        return new TournamentMatchmakingStrategyResolver(new ITournamentMatchmakingStrategy[]
        {
            new SingleEliminationBracketMatchmakingStrategy(),
            new GroupStageMatchmakingStrategy()
        });
    }

    [Fact]
    public async Task Does_nothing_when_no_pair()
    {
        var ctx = Context;

        var duelManager = new DuelManager();

        var taskService = new Mock<ITaskService>();
        var ratingManager = new Mock<IRatingManager>();

        var taski = new TaskiClientSuccessFake();
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
        (await ctx.OutboxMessages.AsNoTracking().ToListAsync())
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Creates_duel_and_sends_messages_when_pair_exists()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        u1.Rating = 1500;
        u2.Rating = 1500;
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();

        var taski = new TaskiClientSuccessFake(["TASK-42"]);

        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                It.IsAny<IReadOnlySet<string>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((DuelConfiguration _, IReadOnlyCollection<DuelTask> _, IReadOnlySet<string> _, out Dictionary<char, DuelTask> chosen) =>
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
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
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

        var messages = await ctx.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType.SendMessage)
            .ToListAsync();

        messages.Should().HaveCount(2);

        foreach (var m in messages)
        {
            m.Status.Should().Be(OutboxStatus.ToDo);
            m.RetryUntil.Should().Be(duel.DeadlineTime.AddMinutes(5));

            var p = ReadSendPayload(m);
            p.Message.Should().BeOfType<DuelStartedMessage>()
                .Which.DuelId.Should().Be(duel.Id);
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
            ShouldShowOpponentSolution = false,
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
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = configuration,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();

        var taski = new TaskiClientSuccessFake(["TASK-99"]);

        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                It.IsAny<IReadOnlySet<string>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((DuelConfiguration _, IReadOnlyCollection<DuelTask> _, IReadOnlySet<string> _, out Dictionary<char, DuelTask> chosen) =>
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
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.Include(d => d.Configuration).SingleAsync();
        duel.Configuration.Id.Should().Be(configuration.Id);
        duel.Tasks.Should().ContainKey('B');
    }

    [Fact]
    public async Task Attaches_created_duel_id_to_tournament_bracket_node()
    {
        var ctx = Context;

        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        ctx.Users.AddRange(creator, user1, user2);

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Nodes = new List<SingleEliminationBracketNode?>
            {
                new(),
                new() { UserId = user1.Id, WinnerUserId = user1.Id },
                new() { UserId = user2.Id, WinnerUserId = user2.Id }
            }
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });

        ctx.Tournaments.Add(tournament);
        ctx.PendingDuels.Add(new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            IsAcceptedByUser2 = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        var taski = new TaskiClientSuccessFake(["TASK-7"]);

        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                It.IsAny<IReadOnlySet<string>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((DuelConfiguration _, IReadOnlyCollection<DuelTask> _, IReadOnlySet<string> _, out Dictionary<char, DuelTask> chosen) =>
            {
                chosen = new Dictionary<char, DuelTask>
                {
                    ['A'] = new("TASK-7", 1, [])
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
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);

        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var duel = await ctx.Duels.SingleAsync();
        var updatedTournament = await ctx.Tournaments
            .OfType<SingleEliminationBracketTournament>()
            .AsNoTracking()
            .SingleAsync();
        updatedTournament.Nodes[0]!.DuelId.Should().Be(duel.Id);
    }

    [Fact]
    public async Task Does_not_create_tournament_duel_until_both_users_accept()
    {
        var ctx = Context;

        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        ctx.Users.AddRange(creator, user1, user2);

        var tournament = new SingleEliminationBracketTournament
        {
            Name = "Cup",
            Status = TournamentStatus.InProgress,
            Group = EntityFactory.MakeGroup(1, "Alpha"),
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Nodes = new List<SingleEliminationBracketNode?>
            {
                new(),
                new() { UserId = user1.Id, WinnerUserId = user1.Id },
                new() { UserId = user2.Id, WinnerUserId = user2.Id }
            }
        };
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user1, Seed = 1 });
        tournament.Participants.Add(new TournamentParticipant { Tournament = tournament, User = user2, Seed = 2 });

        ctx.Tournaments.Add(tournament);
        ctx.PendingDuels.Add(new TournamentPendingDuel
        {
            Type = PendingDuelType.Tournament,
            Tournament = tournament,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAcceptedByUser1 = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();
        var taski = new TaskiClientSuccessFake(["TASK-7"]);

        var taskService = new Mock<ITaskService>();
        var ratingManager = new Mock<IRatingManager>();
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);

        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        (await ctx.Duels.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Removes_used_pending_duels_after_creation()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();

        var taski = new TaskiClientSuccessFake(["TASK-1"]);
        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                It.IsAny<IReadOnlySet<string>>(),
                out It.Ref<Dictionary<char, DuelTask>>.IsAny))
            .Returns(true)
            .Callback(new TryChooseTasksCallback((DuelConfiguration _, IReadOnlyCollection<DuelTask> _, IReadOnlySet<string> _, out Dictionary<char, DuelTask> chosen) =>
            {
                chosen = new Dictionary<char, DuelTask>
                {
                    ['A'] = new("TASK-1", 1, [])
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
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);

        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        ctx.PendingDuels.OfType<RankedPendingDuel>().Should().BeEmpty();
    }

    [Fact]
    public async Task Creates_parallel_tournament_duels_with_reused_catalog_task()
    {
        var ctx = Context;

        var creator = EntityFactory.MakeUser(1, "creator");
        var users = Enumerable.Range(2, 6)
            .Select(id => EntityFactory.MakeUser(id, $"u{id}"))
            .ToArray();
        var group = EntityFactory.MakeGroup(1, "Alpha");
        var tournament = new GroupStageTournament
        {
            Name = "Group stage",
            Status = TournamentStatus.InProgress,
            Group = group,
            CreatedBy = creator,
            CreatedAt = DateTime.UtcNow,
            MatchmakingType = TournamentMatchmakingType.GroupStage
        };
        foreach (var (user, index) in users.Select((user, index) => (user, index)))
        {
            tournament.Participants.Add(new TournamentParticipant
            {
                Tournament = tournament,
                User = user,
                Seed = index + 1
            });
        }

        ctx.Users.Add(creator);
        ctx.Users.AddRange(users);
        ctx.Groups.Add(group);
        ctx.Tournaments.Add(tournament);
        ctx.PendingDuels.AddRange(
            new TournamentPendingDuel
            {
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = users[0],
                User2 = users[1],
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = users[2],
                User2 = users[3],
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow
            },
            new TournamentPendingDuel
            {
                Type = PendingDuelType.Tournament,
                Tournament = tournament,
                User1 = users[4],
                User2 = users[5],
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow
            });
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(m => m.GetTaskLevel(It.IsAny<int>())).Returns(1);
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var taski = new TaskiClientSuccessFake(["TASK-1"]);
        var handler = new TryCreateDuelHandler(
            new DuelManager(),
            taski,
            options,
            ratingManager.Object,
            new TaskService(),
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);

        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var taskIds = await ctx.Duels
            .AsNoTracking()
            .Select(d => d.Tasks['A'].Id)
            .ToListAsync();
        taskIds.Should().HaveCount(3);
        taskIds.Should().OnlyContain(taskId => taskId == "TASK-1");
        taski.GetTasksListCalls.Should().Be(1);
    }

    [Fact]
    public async Task Avoids_tasks_from_users_previous_duels()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var previousDuel = EntityFactory.MakeDuel(10, u1, u2, "TASK-1");
        previousDuel.Status = DuelStatus.Finished;
        previousDuel.EndTime = DateTime.UtcNow.AddMinutes(-1);
        ctx.Users.AddRange(u1, u2);
        ctx.Duels.Add(previousDuel);
        ctx.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = u1,
            User2 = u2,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var ratingManager = new Mock<IRatingManager>();
        var options = Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping = []
        });

        var handler = new TryCreateDuelHandler(
            new DuelManager(),
            new TaskiClientSuccessFake(["TASK-1", "TASK-2"]),
            options,
            ratingManager.Object,
            new TaskService(),
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);

        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        var newDuel = await ctx.Duels
            .AsNoTracking()
            .Where(d => d.Id != previousDuel.Id)
            .SingleAsync();
        newDuel.Tasks['A'].Id.Should().Be("TASK-2");
    }

    [Fact]
    public async Task Fails_when_tasks_not_selected()
    {
        var ctx = Context;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        u1.Rating = 1500;
        u2.Rating = 1500;
        ctx.Users.AddRange(u1, u2);
        ctx.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u1,
                Rating = u1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = u2,
                Rating = u2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            });
        await ctx.SaveChangesAsync();

        var duelManager = new DuelManager();

        var taski = new TaskiClientSuccessFake(["RANDOM_TASK"]);

        var taskService = new Mock<ITaskService>();
        taskService.Setup(s => s.TryChooseTasks(
                It.IsAny<DuelConfiguration>(),
                It.IsAny<IReadOnlyCollection<DuelTask>>(),
                It.IsAny<IReadOnlySet<string>>(),
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
            duelManager,
            taski,
            options,
            ratingManager.Object,
            taskService.Object,
            CreateTournamentStrategyResolver(),
            ctx,
            NullLogger<TryCreateDuelHandler>.Instance);
        var res = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        (await ctx.Duels.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Continues_with_other_pairs_when_one_pair_cannot_select_tasks()
    {
        var users = Enumerable.Range(1, 4)
            .Select(id => EntityFactory.MakeUser(id, $"u{id}"))
            .ToArray();
        var impossibleConfiguration = new DuelConfiguration
        {
            Id = 50,
            Owner = users[0],
            MaxDurationMinutes = 30,
            IsRated = false,
            ShouldShowOpponentSolution = true,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] },
                ['B'] = new() { Level = 1, Topics = [] }
            }
        };
        Context.Users.AddRange(users);
        Context.DuelConfigurations.Add(impossibleConfiguration);
        Context.PendingDuels.AddRange(
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = users[0],
                User2 = users[1],
                Configuration = impossibleConfiguration,
                IsAccepted = true,
                CreatedAt = DateTime.UtcNow
            },
            new FriendlyPendingDuel
            {
                Type = PendingDuelType.Friendly,
                User1 = users[2],
                User2 = users[3],
                Configuration = null,
                IsAccepted = true,
                CreatedAt = DateTime.UtcNow
            });
        await Context.SaveChangesAsync();

        var taski = new TaskiClientSuccessFake(["TASK-1"]);
        var handler = CreateHandler(Context, taski);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        taski.GetTasksListCalls.Should().Be(1);
        var duel = await Context.Duels
            .AsNoTracking()
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleAsync();
        new[] { duel.User1.Id, duel.User2.Id }.Should().BeEquivalentTo([users[2].Id, users[3].Id]);
        var remaining = await Context.PendingDuels
            .OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .Include(pending => pending.User1)
            .Include(pending => pending.User2)
            .SingleAsync();
        new[] { remaining.User1.Id, remaining.User2.Id }.Should().BeEquivalentTo([users[0].Id, users[1].Id]);
    }

    [Fact]
    public async Task Keeps_pending_pair_when_taski_catalog_fails()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        Context.Users.AddRange(user1, user2);
        Context.PendingDuels.AddRange(
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user1,
                Rating = user1.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new RankedPendingDuel
            {
                Type = PendingDuelType.Ranked,
                User = user2,
                Rating = user2.Rating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            });
        await Context.SaveChangesAsync();

        var taski = new Mock<ITaskiClient>();
        taski.Setup(client => client.GetTasksListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<TaskListResponse>("catalog unavailable"));
        var handler = CreateHandler(Context, taski.Object);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        (await Context.PendingDuels.AsNoTracking().CountAsync()).Should().Be(2);
        (await Context.Duels.AsNoTracking().CountAsync()).Should().Be(0);
        (await Context.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Keeps_pending_pair_when_a_user_already_has_an_active_duel()
    {
        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        var activeDuel = EntityFactory.MakeDuel(10, user1, user2);
        Context.Users.AddRange(user1, user2);
        Context.Duels.Add(activeDuel);
        Context.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var taski = new TaskiClientSuccessFake(["TASK-2"]);
        var handler = CreateHandler(Context, taski);

        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await Context.Duels.AsNoTracking().CountAsync()).Should().Be(1);
        (await Context.PendingDuels.AsNoTracking().CountAsync()).Should().Be(1);
        (await Context.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rolls_back_duel_link_and_outbox_and_stops_tick_when_second_save_fails()
    {
        var interceptor = new FailOnArmedSaveChangesInterceptor(2);
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new Context(options);
        await context.Database.EnsureCreatedAsync();

        var creator = EntityFactory.MakeUser(1, "creator");
        var user1 = EntityFactory.MakeUser(2, "u1");
        var user2 = EntityFactory.MakeUser(3, "u2");
        var user3 = EntityFactory.MakeUser(4, "u3");
        var user4 = EntityFactory.MakeUser(5, "u4");
        var group = EntityFactory.MakeGroup(1, "group");
        context.Users.AddRange(creator, user1, user2, user3, user4);
        context.Groups.Add(group);
        context.PendingDuels.AddRange(
            new GroupPendingDuel
            {
                Type = PendingDuelType.Group,
                Group = group,
                CreatedBy = creator,
                User1 = user1,
                User2 = user2,
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow
            },
            new GroupPendingDuel
            {
                Type = PendingDuelType.Group,
                Group = group,
                CreatedBy = creator,
                User1 = user3,
                User2 = user4,
                Configuration = null,
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = true,
                CreatedAt = DateTime.UtcNow.AddSeconds(1)
            });
        await context.SaveChangesAsync();
        interceptor.Arm();

        var handler = CreateHandler(context, new TaskiClientSuccessFake(["TASK-1"]));
        var result = await handler.Handle(new TryCreateDuelCommand(), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        (await context.Duels.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.GroupDuels.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.PendingDuels.OfType<GroupPendingDuel>().AsNoTracking().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Concurrent_ticks_create_only_one_duel_for_the_same_pair()
    {
        var connectionString = $"Data Source=duel-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keepAliveConnection = new SqliteConnection(connectionString);
        await keepAliveConnection.OpenAsync();
        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connectionString)
            .Options;
        await using var setupContext = new Context(options);
        await setupContext.Database.EnsureCreatedAsync();

        var user1 = EntityFactory.MakeUser(1, "u1");
        var user2 = EntityFactory.MakeUser(2, "u2");
        setupContext.Users.AddRange(user1, user2);
        setupContext.PendingDuels.Add(new FriendlyPendingDuel
        {
            Type = PendingDuelType.Friendly,
            User1 = user1,
            User2 = user2,
            Configuration = null,
            IsAccepted = true,
            CreatedAt = DateTime.UtcNow
        });
        await setupContext.SaveChangesAsync();

        var bothTicksLoadedPair = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalogCalls = 0;
        var taski = new Mock<ITaskiClient>();
        taski.Setup(client => client.GetTasksListAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken cancellationToken) =>
            {
                if (Interlocked.Increment(ref catalogCalls) == 2)
                {
                    bothTicksLoadedPair.SetResult();
                }

                await bothTicksLoadedPair.Task.WaitAsync(cancellationToken);
                return Result.Ok(new TaskListResponse
                {
                    Tasks =
                    [
                        new TaskResponse
                        {
                            Id = "TASK-1",
                            Level = 1,
                            Topics = []
                        }
                    ]
                });
            });

        await using var context1 = new Context(options);
        await using var context2 = new Context(options);
        var handler1 = CreateHandler(context1, taski.Object);
        var handler2 = CreateHandler(context2, taski.Object);

        await Task.WhenAll(
            handler1.Handle(new TryCreateDuelCommand(), CancellationToken.None),
            handler2.Handle(new TryCreateDuelCommand(), CancellationToken.None));

        (await setupContext.Duels.AsNoTracking().CountAsync()).Should().Be(1);
        (await setupContext.PendingDuels.AsNoTracking().CountAsync()).Should().Be(0);
        (await setupContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(2);
    }

    private static TryCreateDuelHandler CreateHandler(
        Context context,
        ITaskiClient taskiClient)
    {
        var ratingManager = new Mock<IRatingManager>();
        ratingManager.Setup(manager => manager.GetTaskLevel(It.IsAny<int>())).Returns(1);

        return new TryCreateDuelHandler(
            new DuelManager(),
            taskiClient,
            Options.Create(new DuelOptions
            {
                DefaultMaxDurationMinutes = 30,
                RatingToTaskLevelMapping = []
            }),
            ratingManager.Object,
            new TaskService(),
            CreateTournamentStrategyResolver(),
            context,
            NullLogger<TryCreateDuelHandler>.Instance);
    }

    private sealed class FailOnArmedSaveChangesInterceptor(int failOnCall) : SaveChangesInterceptor
    {
        private bool _armed;
        private int _saveCalls;

        public void Arm()
        {
            _saveCalls = 0;
            _armed = true;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed && Interlocked.Increment(ref _saveCalls) == failOnCall)
            {
                throw new InvalidOperationException("Injected failure between duel creation steps.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private delegate void TryChooseTasksCallback(
        DuelConfiguration configuration,
        IReadOnlyCollection<DuelTask> tasks,
        IReadOnlySet<string> excludedTaskIds,
        out Dictionary<char, DuelTask> chosenTasks);
}
