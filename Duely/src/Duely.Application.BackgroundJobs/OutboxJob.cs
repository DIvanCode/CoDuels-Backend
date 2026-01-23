using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models.Outbox;
using Duely.Infrastructure.DataAccess.EntityFramework;
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
        logger.LogDebug("OutboxJob started. IntervalMs = {IntervalMs}", options.Value.CheckIntervalMs);
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var now = DateTime.UtcNow;

                var deleted = await db.OutboxMessages
                    .Where(m => m.RetryUntil <= now)
                    .ExecuteDeleteAsync(cancellationToken);
                if (deleted > 0)
                {
                    logger.LogDebug("Outbox expired messages deleted. Count = {Count}", deleted);
                }

                var message = await db.OutboxMessages
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
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    message.Status = OutboxStatus.InProgress;
                    await db.SaveChangesAsync(cancellationToken);

                    logger.LogDebug("Outbox dispatch start. MessageId = {MessageId}, Type = {Type}, Retries = {Retries}",
                        message.Id, message.Type, message.Retries
                    );

                    var result = await dispatcher.DispatchAsync(message, cancellationToken);
                    if (result.IsFailed)
                    {
                        logger.LogWarning("Outbox dispatch failed: {Reason}",
                            string.Join("\n", result.Errors.Select(error => error.Message)));
                    }

                    var processedAt = DateTime.UtcNow;
                    if (processedAt >= message.RetryUntil || result.IsSuccess)
                    {
                        db.OutboxMessages.Remove(message);
                    }
                    else
                    {
                        message.Retries++;
                        message.Status = OutboxStatus.ToRetry;
                        var retryDelayMs = CalculateRetryDelayMs(
                            options.Value.InitialRetryDelayMs,
                            options.Value.MaxRetryDelayMs,
                            message.Retries);
                        message.RetryAt = processedAt.AddMilliseconds(retryDelayMs);

                        logger.LogDebug(
                            "Outbox retry scheduled. MessageId = {MessageId}, Retries = {Retries}, RetryAt = {RetryAt}",
                            message.Id, message.Retries, message.RetryAt
                        );
                    }

                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(options.Value.CheckIntervalMs), cancellationToken);
        }
    }
    
    private static long CalculateRetryDelayMs(int initialDelayMs, int maxDelayMs, int retries)
    {
        if (retries <= 0)
            return initialDelayMs;

        var delay = (long)initialDelayMs << (retries - 1);
        if (delay > maxDelayMs)
            delay = maxDelayMs;

        return delay;
    }
}
