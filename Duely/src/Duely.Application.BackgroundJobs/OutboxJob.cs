using Duely.Application.UseCases.Features.Outbox;     
using Duely.Domain.Models;                
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Features.Outbox.Relay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs;

public sealed class OutboxJob(IServiceProvider sp, IOptions<OutboxOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var outboxOptions = options.Value;

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var now = DateTime.UtcNow;
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

                    var result = await dispatcher.DispatchAsync(message, cancellationToken);

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
