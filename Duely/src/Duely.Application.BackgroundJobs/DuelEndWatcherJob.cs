using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;
using Microsoft.Extensions.Logging;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelEndWatcherJob(IServiceProvider sp, IOptions<DuelEndWatcherJobOptions> options, ILogger<DuelEndWatcherJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DuelEndWatcherJob started. IntervalMs = {IntervalMs}", options.Value.CheckIntervalMs);
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new CheckDuelsForFinishCommand(), cancellationToken);
            }

            await Task.Delay(options.Value.CheckIntervalMs, cancellationToken);
        }
    }
}
