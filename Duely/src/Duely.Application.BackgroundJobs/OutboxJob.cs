using Duely.Application.UseCases.Features.Outbox;     
using Duely.Domain.Models;                
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
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
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var now = DateTime.UtcNow;
                var messages = await db.Outbox
                    .Where(m => m.Status == OutboxStatus.ToDo || (m.Status == OutboxStatus.ToRetry && m.RetryAt != null && m.RetryAt <= now) || (m.Status == OutboxStatus.InProgress && m.RetryAt != null && m.RetryAt <= now))
                    .OrderBy(m => m.Id)
                    .ToListAsync(cancellationToken);
                if (messages.Count > 0)
                {
                    foreach (var m in messages)
                    {
                        m.Status  = OutboxStatus.InProgress;
                        m.RetryAt = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync(cancellationToken);
                    foreach (var m in messages)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var result = await mediator.Send(
                            new ExecuteOutboxMessageCommand(m), cancellationToken);

                        if (result.IsSuccess)
                        {
                            db.Outbox.Remove(m);
                        }
                        else
                        {
                            m.Retries++;
                            m.Status = OutboxStatus.ToRetry;
                            m.RetryAt = DateTime.UtcNow.AddSeconds(outboxOptions.RetryDelayMs);
                        }
                    
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            await Task.Delay(outboxOptions.CheckIntervalMs, cancellationToken);
        }
    }
}
