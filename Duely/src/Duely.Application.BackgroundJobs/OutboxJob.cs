using Duely.Application.UseCases.Features.Outbox;     
using Duely.Domain.Models;                
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Features.Outbox.Relay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Duely.Application.BackgroundJobs;

public sealed class OutboxJob(IServiceProvider sp, IOptions<OutboxOptions> options, ILogger<OutboxJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var outboxOptions = options.Value;

        logger.LogInformation("OutboxJob started. IntervalMs = {IntervalMs}", outboxOptions.CheckIntervalMs);

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var now = DateTime.UtcNow;

                var deleted = await db.Outbox
                    .Where(m => m.RetryUntil <= now)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleted > 0)
                {
                    logger.LogInformation("Outbox expired messages deleted. Count = {Count}", deleted);
                }

                now = DateTime.UtcNow;
                var message = await db.Outbox
                    .Where(m => m.Status == OutboxStatus.ToDo 
                    && (m.RetryUntil > now)
                    || (m.Status == OutboxStatus.ToRetry
                        && m.RetryAt != null
                        && m.RetryAt <= now
                        && m.RetryUntil > now)
                    || (m.Status == OutboxStatus.InProgress
                        && m.RetryAt != null
                        && m.RetryAt <= now
                        && m.RetryUntil > now))
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (message is not null)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    message.Status = OutboxStatus.InProgress;
                    await db.SaveChangesAsync(cancellationToken);

                    logger.LogDebug("Outbox dispatch start. MessageId = {MessageId}, Type = {Type}, Retries = {Retries}",
                        message.Id, message.Type, message.Retries
                    );

                    var result = await dispatcher.DispatchAsync(message, cancellationToken);

                    if (result.IsFailed)
                    {
                        logger.LogWarning("Outbox dispatch failed: {Reason}", string.Join("\n", result.Errors.Select(error => error.Message)));
                    }

                    var processedAt = DateTime.UtcNow;

                    if (processedAt >= message.RetryUntil || result.IsSuccess)
                    {
                        db.Outbox.Remove(message);
                    }
                    else
                    {
                        message.Retries++;
                        message.Status = OutboxStatus.ToRetry;
                        var RetryDelayMs = CalculateRetryDelayMs(
                            outboxOptions.InitialRetryDelayMs,
                            outboxOptions.MaxRetryDelayMs,
                            message.Retries);
                        message.RetryAt = processedAt.AddMilliseconds(RetryDelayMs);

                        logger.LogDebug("Outbox retry scheduled. MessageId = {MessageId}, Type = {Type}, Retries = {Retries}, RetryAt = {RetryAt}",
                            message.Id, message.Type, message.Retries, message.RetryAt
                        );
                    }
                
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(outboxOptions.CheckIntervalMs),cancellationToken);
        }
    }
    private static int CalculateRetryDelayMs(int initialDelayMs, int maxDelayMs, int retries)
    {
        if (retries <= 0)
            return initialDelayMs;

        long delay = (long)initialDelayMs << (retries - 1);
        if (delay > maxDelayMs)
            delay = maxDelayMs;

        return (int)delay;
    }
}
