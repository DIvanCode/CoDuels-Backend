using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.UserActions;
using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class CheckDuelsForAnticheatHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Processes_only_scores_with_null_score_and_sets_score()
    {
        var ctx = Context;
        var now = DateTime.UtcNow;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = new Duel
        {
            Id = 100,
            Configuration = new DuelConfiguration
            {
                Id = 100,
                Owner = u1,
                MaxDurationMinutes = 30,
                IsRated = true,
                ShouldShowOpponentSolution = false,
                TasksCount = 2,
                TasksOrder = DuelTasksOrder.Sequential,
                TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
                {
                    ['A'] = new() { Level = 1, Topics = [] },
                    ['B'] = new() { Level = 1, Topics = [] }
                }
            },
            Status = DuelStatus.Finished,
            Tasks = new Dictionary<char, DuelTask>
            {
                ['A'] = new("TASK-A", 1, []),
                ['B'] = new("TASK-B", 1, [])
            },
            StartTime = now.AddMinutes(-40),
            DeadlineTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(-5),
            User1 = u1,
            User2 = u2,
            User1InitRating = 1500,
            User2InitRating = 1500
        };

        ctx.AddRange(
            u1,
            u2,
            duel,
            new AnticheatScore
            {
                Duel = duel,
                User = u1,
                TaskKey = 'A',
                Score = null
            },
            new AnticheatScore
            {
                Duel = duel,
                User = u1,
                TaskKey = 'B',
                Score = null
            },
            new AnticheatScore
            {
                Duel = duel,
                User = u2,
                TaskKey = 'A',
                Score = null
            },
            new AnticheatScore
            {
                Duel = duel,
                User = u2,
                TaskKey = 'B',
                Score = null
            },
            new WriteCodeUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 1,
                Timestamp = now.AddMinutes(-20),
                DuelId = duel.Id,
                TaskKey = 'A',
                UserId = u1.Id,
                CodeLength = 42,
                CursorLine = 4
            },
            new RunSampleTestUserAction
            {
                EventId = Guid.NewGuid(),
                SequenceId = 1,
                Timestamp = now.AddMinutes(-19),
                DuelId = duel.Id,
                TaskKey = 'B',
                UserId = u2.Id
            });
        await ctx.SaveChangesAsync();

        var analyzerClient = new Mock<IAnalyzerClient>();
        analyzerClient
            .Setup(client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(Result.Ok(new PredictResponse
            {
                Score = 0.7f
            })));

        var handler = new CheckDuelsForAnticheatHandler(ctx, analyzerClient.Object);

        var result = await handler.Handle(new CheckDuelsForAnticheatCommand(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var scores = await ctx.AnticheatScores
            .AsNoTracking()
            .Include(score => score.Duel)
            .Where(score => score.Duel.Id == duel.Id)
            .ToListAsync();
        var remainingActions = await ctx.UserActions
            .AsNoTracking()
            .Where(action => action.DuelId == duel.Id)
            .ToListAsync();

        scores.Should().HaveCount(4);
        scores.Should().OnlyContain(
            score => score.Score.HasValue && Math.Abs(score.Score.Value - 0.7f) < 0.0001f,
            $"actual scores: {string.Join(", ", scores.Select(score => score.Score?.ToString() ?? "null"))}");
        remainingActions.Should().BeEmpty();

        analyzerClient.Verify(
            client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task Skips_scores_with_value_and_processes_only_null_scores()
    {
        var ctx = Context;
        var now = DateTime.UtcNow;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(200, u1, u2, "TASK-1", now.AddMinutes(-30), now.AddMinutes(-1));
        duel.Status = DuelStatus.Finished;
        duel.EndTime = now;

        ctx.AddRange(
            u1,
            u2,
            duel,
            new AnticheatScore
            {
                Duel = duel,
                User = u1,
                TaskKey = 'A',
                Score = 0.1f
            },
            new AnticheatScore
            {
                Duel = duel,
                User = u2,
                TaskKey = 'A',
                Score = null
            });
        await ctx.SaveChangesAsync();

        var analyzerClient = new Mock<IAnalyzerClient>();
        analyzerClient
            .Setup(client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(Result.Ok(new PredictResponse
            {
                Score = 0.9f
            })));

        var handler = new CheckDuelsForAnticheatHandler(ctx, analyzerClient.Object);

        var result = await handler.Handle(new CheckDuelsForAnticheatCommand(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var scores = await ctx.AnticheatScores
            .AsNoTracking()
            .Include(score => score.Duel)
            .Include(score => score.User)
            .Where(score => score.Duel.Id == duel.Id)
            .ToListAsync();
        var remainingActions = await ctx.UserActions
            .AsNoTracking()
            .Where(action => action.DuelId == duel.Id)
            .ToListAsync();

        scores.Should().HaveCount(2);
        scores.Should().Contain(score =>
            score.User.Id == u1.Id &&
            score.TaskKey == 'A' &&
            score.Score.HasValue &&
            Math.Abs(score.Score.Value - 0.1f) < 0.0001f);
        scores.Should().Contain(score =>
            score.User.Id == u2.Id &&
            score.TaskKey == 'A' &&
            score.Score.HasValue &&
            Math.Abs(score.Score.Value - 0.9f) < 0.0001f);
        remainingActions.Should().BeEmpty();

        analyzerClient.Verify(
            client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Processes_unchecked_scores_from_later_duels()
    {
        var ctx = Context;
        var now = DateTime.UtcNow;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        ctx.AddRange(u1, u2);

        for (var i = 1; i <= 21; i++)
        {
            var duel = EntityFactory.MakeDuel(300 + i, u1, u2, $"TASK-{i}", now.AddMinutes(-100 - i), now.AddMinutes(-90 - i));
            duel.Status = DuelStatus.Finished;
            duel.EndTime = now.AddMinutes(-80 - i);

            ctx.Add(duel);
            ctx.AnticheatScores.AddRange(
                new AnticheatScore
                {
                    Duel = duel,
                    User = u1,
                    TaskKey = 'A',
                    Score = 0.2f
                },
                new AnticheatScore
                {
                    Duel = duel,
                    User = u2,
                    TaskKey = 'A',
                    Score = 0.3f
                });
        }

        var pendingDuel = EntityFactory.MakeDuel(999, u1, u2, "TASK-PENDING", now.AddMinutes(-50), now.AddMinutes(-40));
        pendingDuel.Status = DuelStatus.Finished;
        pendingDuel.EndTime = now.AddMinutes(-30);
        ctx.AddRange(
            pendingDuel,
            new AnticheatScore
            {
                Duel = pendingDuel,
                User = u1,
                TaskKey = 'A',
                Score = null
            },
            new AnticheatScore
            {
                Duel = pendingDuel,
                User = u2,
                TaskKey = 'A',
                Score = null
            });

        await ctx.SaveChangesAsync();

        var analyzerClient = new Mock<IAnalyzerClient>();
        analyzerClient
            .Setup(client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(Result.Ok(new PredictResponse
            {
                Score = 0.95f
            })));

        var handler = new CheckDuelsForAnticheatHandler(ctx, analyzerClient.Object);

        var result = await handler.Handle(new CheckDuelsForAnticheatCommand(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var pendingScores = await ctx.AnticheatScores
            .AsNoTracking()
            .Include(score => score.Duel)
            .Where(score => score.Duel.Id == pendingDuel.Id)
            .ToListAsync();
        var pendingActions = await ctx.UserActions
            .AsNoTracking()
            .Where(action => action.DuelId == pendingDuel.Id)
            .ToListAsync();

        pendingScores.Should().HaveCount(2);
        pendingScores.Should().OnlyContain(
            score => score.Score.HasValue && Math.Abs(score.Score.Value - 0.95f) < 0.0001f,
            $"actual pending scores: {string.Join(", ", pendingScores.Select(score => score.Score?.ToString() ?? "null"))}");
        pendingActions.Should().BeEmpty();

        analyzerClient.Verify(
            client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Does_not_delete_actions_when_cleanup_disabled()
    {
        var ctx = Context;
        var now = DateTime.UtcNow;

        var u1 = EntityFactory.MakeUser(1, "u1");
        var u2 = EntityFactory.MakeUser(2, "u2");
        var duel = EntityFactory.MakeDuel(777, u1, u2, "TASK-1", now.AddMinutes(-30), now.AddMinutes(-1));
        duel.Status = DuelStatus.Finished;
        duel.EndTime = now;

        var action = new WriteCodeUserAction
        {
            EventId = Guid.NewGuid(),
            SequenceId = 1,
            Timestamp = now.AddMinutes(-20),
            DuelId = duel.Id,
            TaskKey = 'A',
            UserId = u1.Id,
            CodeLength = 42,
            CursorLine = 4
        };

        ctx.AddRange(
            u1,
            u2,
            duel,
            action,
            new AnticheatScore
            {
                Duel = duel,
                User = u1,
                TaskKey = 'A',
                Score = null
            });
        await ctx.SaveChangesAsync();

        var analyzerClient = new Mock<IAnalyzerClient>();
        analyzerClient
            .Setup(client => client.PredictAsync(It.IsAny<PredictRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(Result.Ok(new PredictResponse
            {
                Score = 0.5f
            })));

        var handler = new CheckDuelsForAnticheatHandler(ctx, analyzerClient.Object);

        var result = await handler.Handle(new CheckDuelsForAnticheatCommand(false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var score = await ctx.AnticheatScores
            .AsNoTracking()
            .Include(s => s.Duel)
            .Include(s => s.User)
            .SingleAsync(s => s.Duel.Id == duel.Id && s.User.Id == u1.Id && s.TaskKey == 'A');
        var remainingActions = await ctx.UserActions
            .AsNoTracking()
            .Where(a => a.DuelId == duel.Id && a.UserId == u1.Id && a.TaskKey == 'A')
            .ToListAsync();

        score.Score.Should().NotBeNull();
        remainingActions.Should().ContainSingle(a => a.EventId == action.EventId);
    }
}
