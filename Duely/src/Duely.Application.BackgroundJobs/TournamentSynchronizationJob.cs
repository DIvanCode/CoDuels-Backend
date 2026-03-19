using Duely.Application.UseCases.Features.Tournaments;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs;

public sealed class TournamentSynchronizationJob(
    IServiceProvider sp,
    IOptions<TournamentSynchronizationJobOptions> options,
    ILogger<TournamentSynchronizationJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "TournamentSynchronizationJob started. IntervalMs = {IntervalMs}",
            options.Value.CheckIntervalMs);

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new SyncActiveTournamentsCommand(), cancellationToken);
                if (result.IsFailed)
                {
                    logger.LogWarning(
                        "failed to synchronize tournaments: {Reason}",
                        string.Join("\n", result.Errors.Select(error => error.Message)));
                }
            }

            await Task.Delay(options.Value.CheckIntervalMs, cancellationToken);
        }
    }
}
