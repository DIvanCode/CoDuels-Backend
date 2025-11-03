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
                    || (m.Status == OutboxStatus.ToRetry
                        && m.RetryAt != null
                        && m.RetryAt <= now)
                    || (m.Status == OutboxStatus.InProgress
                        && m.RetryAt != null
                        && m.RetryAt <= now))
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (message is null)
                {
                    await Task.Delay(outboxOptions.CheckIntervalMs, cancellationToken);
                    continue;
                }
                if (cancellationToken.IsCancellationRequested) break;

                var result = await dispatcher.DispatchAsync(message, cancellationToken);

                if (result.IsSuccess)
                {
                    db.Outbox.Remove(message);
                }
                else
                {
                    message.Retries++;
                    message.Status = OutboxStatus.ToRetry;
                    message.RetryAt = DateTime.UtcNow.AddMilliseconds(outboxOptions.RetryDelayMs);
                }
            
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
