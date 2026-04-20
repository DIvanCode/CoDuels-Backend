using Duely.Application.UseCases.Features.Duels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs;

public sealed class AnticheatBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<AnticheatBackgroundServiceOptions> options,
    ILogger<AnticheatBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AnticheatBackgroundService started. IntervalMs = {IntervalMs}, ShouldCleanupUserActions = {ShouldCleanupUserActions}",
            options.Value.CheckIntervalMs,
            options.Value.ShouldCleanupUserActions);

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(
                    new CheckDuelsForAnticheatCommand(options.Value.ShouldCleanupUserActions),
                    cancellationToken);
                if (result.IsFailed)
                {
                    logger.LogWarning(
                        "failed check duels for anticheat: {Reason}",
                        string.Join("\n", result.Errors.Select(error => error.Message)));
                }
            }

            await Task.Delay(options.Value.CheckIntervalMs, cancellationToken);
        }
    }
}
