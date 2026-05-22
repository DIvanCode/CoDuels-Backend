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
        logger.LogInformation("OutboxJob started. IntervalMs = {IntervalMs}", options.Value.CheckIntervalMs);
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var now = DateTime.UtcNow;
                
                await DeleteOldMessages(now, db, cancellationToken);
                var messages = await ClaimNewMessages(now, db, cancellationToken);

                now = DateTime.UtcNow;
                await ProcessMessages(now, messages, db, dispatcher, cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(options.Value.CheckIntervalMs), cancellationToken);
        }
    }

    private async Task DeleteOldMessages(DateTime now, Context db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleted = await db.OutboxMessages
                .Where(m => m.RetryUntil <= now)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                logger.LogDebug("Outbox expired messages deleted. Count = {Count}", deleted);
            }
            
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete old outbox messages");
            await transaction.RollbackAsync(cancellationToken);
        }
    }
    
    private async Task<List<OutboxMessage>> ClaimNewMessages(
        DateTime now,
        Context db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var messages = await db.OutboxMessages
                .Where(m =>
                    m.Status == OutboxStatus.ToDo ||
                    (m.Status == OutboxStatus.ToRetry && m.RetryAt != null && m.RetryAt <= now) ||
                    (m.Status == OutboxStatus.InProgress && m.RetryAt != null && m.RetryAt <= now))
                .OrderBy(m => m.Id)
                .Take(options.Value.BatchSize)
                .ForUpdate()
                .ToListAsync(cancellationToken);
            foreach (var message in messages)
            {
                var retryDelayMs = CalculateRetryDelayMs(
                    options.Value.InitialRetryDelayMs,
                    options.Value.MaxRetryDelayMs,
                    message.Retries);
                message.RetryAt = now.AddMilliseconds(retryDelayMs);
                message.Status = OutboxStatus.InProgress;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to claim outbox messages");
            await transaction.RollbackAsync(cancellationToken);
            return [];
        }
    }

    private async Task ProcessMessages(
        DateTime now,
        List<OutboxMessage> messages,
        Context db,
        IOutboxDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var message in messages)
            {
                logger.LogDebug(
                    "Outbox start. MessageId = {MessageId}, Type = {Type}, Retries = {Retries}",
                    message.Id, message.Type, message.Retries
                );

                var result = await dispatcher.DispatchAsync(message, cancellationToken);
                if (result.IsFailed)
                {
                    var reason = string.Join("\n", result.Errors.Select(error => error.Message)); 
                    logger.LogWarning("Outbox failed: {Reason}", reason);
                        
                    message.Retries++;
                    message.Status = OutboxStatus.ToRetry;
                    var retryDelayMs = CalculateRetryDelayMs(
                        options.Value.InitialRetryDelayMs,
                        options.Value.MaxRetryDelayMs,
                        message.Retries);
                    message.RetryAt = now.AddMilliseconds(retryDelayMs);

                    logger.LogDebug(
                        "Outbox retry. MessageId = {MessageId}, Retries = {Retries}, RetryAt = {RetryAt}",
                        message.Id, message.Retries, message.RetryAt
                    );
                }
                else
                {
                    db.OutboxMessages.Remove(message);
                    logger.LogDebug("Outbox success. MessageId = {MessageId}", message.Id);
                }
            }
            
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process outbox messages");
            await transaction.RollbackAsync(cancellationToken);
        }        
    }
    
    private static long CalculateRetryDelayMs(int initialDelayMs, int maxDelayMs, int retries)
    {
        if (retries <= 0)
        {
            return initialDelayMs;
        }

        var delay = (long)initialDelayMs << (retries - 1);
        if (delay > maxDelayMs)
        {
            delay = maxDelayMs;
        }

        return delay;
    }
}
