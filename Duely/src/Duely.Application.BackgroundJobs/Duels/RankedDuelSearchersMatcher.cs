using Duely.Application.Handlers.Duels.UseCases.RankedDuels;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs.Duels;

internal sealed class RankedDuelSearchersMatcher(
    IOptions<RankedDuelSearchersMatcherOptions> options,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RankedDuelSearchersMatcher> logger)
    : BackgroundService
{
    private const int BaseWindow = 50;
    private const int GrowPerSecond = 10;
    private const int FallbackAfterSeconds = 30;
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Ranked duel searchers matcher started with interval {Interval} ms",
            options.Value.IntervalMs);
        await Task.Yield();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WorkAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ranked duel searchers matcher unexpected error");
            }
            
            await Task.Delay(options.Value.IntervalMs, cancellationToken);
        }
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var candidates = await context.RankedDuelSearchers
            .Include(s => s.User)
            .OrderBy(s => s.User.Rating)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        var pair = FindBestPair(candidates);
        if (pair is null)
        {
            return;
        }

        context.RankedDuelSearchers.Remove(pair.Value.Item1);
        context.RankedDuelSearchers.Remove(pair.Value.Item2);

        var command = new CreateRankedDuelCommand
        {
            Participants = new List<int> {pair.Value.Item1.User.Id, pair.Value.Item2.User.Id}
        };
        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailed)
        {
            logger.LogError(
                "Failed to create ranked duel with users {Participants} due to error: {Error}",
                $"{pair.Value.Item1.User.Nickname}, {pair.Value.Item2.User.Nickname}",
                string.Join(" ", result.Errors.Select(e => e.Message)));
            return;
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }

    private static (RankedDuelSearcher, RankedDuelSearcher)? FindBestPair(List<RankedDuelSearcher> candidates)
    {
        if (candidates.Count < 2)
        {
            return null;
        }
        
        RankedDuelSearcher? bestA = null, bestB = null;
        var bestDiff = int.MaxValue;
        var now = DateTime.UtcNow;
        
        // find pair with min rating diff, but no more than allowed
        for (var i = 0; i < candidates.Count - 1; i++)
        {
            var a = candidates[i];
            var b = candidates[i + 1];
            var diff = Math.Abs(a.User.Rating - b.User.Rating);
            var allowed = Math.Min(GetWindowFor(a, now), GetWindowFor(b, now));
            if (diff > allowed)
            {
                continue;
            }

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestA = a;
                bestB = b;
            }
            else if (diff == bestDiff && bestA is not null && bestB is not null)
            {
                var prevMinWait = Math.Min(
                    (now - bestA.CreatedAt).TotalSeconds,
                    (now - bestB.CreatedAt).TotalSeconds);
                var newMinWait = Math.Min(
                    (now - a.CreatedAt).TotalSeconds,
                    (now - b.CreatedAt).TotalSeconds);
                if (newMinWait > prevMinWait)
                {
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA is not null && bestB is not null)
        {
            return (bestA, bestB);
        }

        bestA = null;
        bestB = null;
        bestDiff = int.MaxValue;

        var oldestWaitingUser = candidates.MinBy(u => u.CreatedAt);
        var oldestWaitSeconds = (now - oldestWaitingUser!.CreatedAt).TotalSeconds;
        if (oldestWaitSeconds < FallbackAfterSeconds)
        {
            return null;
        }
        
        // find pair with min rating diff
        for (var i = 0; i < candidates.Count - 1; i++)
        {
            var a = candidates[i];
            var b = candidates[i + 1];
            var diff = Math.Abs(a.User.Rating - b.User.Rating);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestA = a;
                bestB = b;
            }
            else if (diff == bestDiff && bestA is not null && bestB is not null)
            {
                var prevMinWait = Math.Min(
                    (now - bestA.CreatedAt).TotalSeconds,
                    (now - bestB.CreatedAt).TotalSeconds);
                var newMinWait = Math.Min(
                    (now - a.CreatedAt).TotalSeconds,
                    (now - b.CreatedAt).TotalSeconds);
                if (newMinWait > prevMinWait)
                {
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA is not null && bestB is not null)
        {
            return (bestA, bestB);
        }

        return null;
    }

    private static int GetWindowFor(RankedDuelSearcher candidate, DateTime now)
    {
        var seconds = (now - candidate.CreatedAt).TotalSeconds;
        return BaseWindow + (int) (seconds * GrowPerSecond);
    }
}
