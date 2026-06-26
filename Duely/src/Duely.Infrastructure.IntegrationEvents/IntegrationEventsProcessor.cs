using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.IntegrationEvents;

internal sealed class IntegrationEventsProcessor(
    IOptions<IntegrationEventsProcessorOptions> options,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<IntegrationEventsProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Integration events processor started with interval {Interval} ms",
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
                logger.LogError(ex, "Integration events processor unexpected error");
            }
            
            await Task.Delay(options.Value.IntervalMs, cancellationToken);
        }
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var handlerResolver = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandlerResolver>();

        var integrationEvents = await context.IntegrationEvents
            .Where(e => e.NextProcessAttemptAt <= DateTime.UtcNow)
            .OrderBy(e => e.Id)
            .Take(options.Value.BatchSize)
            .ForUpdate()
            .ToListAsync(cancellationToken);
        foreach (var integrationEvent in integrationEvents)
        {
            var nextAttemptDelayMs = CalculateNextAttemptDelay(integrationEvent.ProcessAttempts);
            integrationEvent.Process(DateTime.UtcNow.AddMilliseconds(nextAttemptDelayMs));
        }
        
        await context.SaveChangesAsync(cancellationToken);
        
        foreach (var integrationEvent in integrationEvents)
        {
            var handler = handlerResolver.Resolve(integrationEvent.Type);
            
            var result = await handler.Handle(integrationEvent, cancellationToken);
            if (result.IsSuccess || (result.IsFailed && result.HasError<IntegrationEventExpiredError>()))
            {
                context.Remove(integrationEvent);
            }
            else
            {
                var nextAttemptDelayMs = CalculateNextAttemptDelay(integrationEvent.ProcessAttempts - 1);
                integrationEvent.Failed(DateTime.UtcNow.AddMilliseconds(nextAttemptDelayMs));
            }
            
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private long CalculateNextAttemptDelay(int attempts)
    {
        if (attempts == 0)
        {
            return options.Value.InitialNextProcessAttemptDelayMs;
        }

        var failedAttempts = Math.Min(attempts - 1, 10);
        var delay = options.Value.InitialNextProcessAttemptDelayMs +
                    options.Value.NextProcessAttemptDelayStepMs * (1 << failedAttempts);
        
        return Math.Min(options.Value.MaxNextProcessAttemptDelayMs, delay);
    }
}
