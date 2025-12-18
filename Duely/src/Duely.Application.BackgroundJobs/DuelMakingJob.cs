using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;
using Microsoft.Extensions.Logging;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob(IServiceProvider sp, IOptions<DuelMakingJobOptions> options, ILogger<DuelMakingJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DuelMakingJob started. IntervalMs = {IntervalMs}", options.Value.CheckPairIntervalMs);
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new TryCreateDuelCommand(), cancellationToken);
                if (result.IsFailed)
                {
                    foreach (var error in result.Errors)
                    {
                        logger.LogWarning("TryCreateDuel failed: {Reason}", error.Message);
                    }
                }
            }

            await Task.Delay(options.Value.CheckPairIntervalMs, cancellationToken);
        }
    }
}
