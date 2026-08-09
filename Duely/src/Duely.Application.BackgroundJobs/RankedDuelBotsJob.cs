using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels.Search;
using Microsoft.Extensions.Logging;

namespace Duely.Application.BackgroundJobs;

public sealed class RankedDuelBotsJob(
    IServiceProvider sp,
    IOptions<RankedDuelBotsJobOptions> options,
    ILogger<RankedDuelBotsJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("RankedDuelBotsJob started. IntervalMs = {IntervalMs}", options.Value.CheckIntervalMs);
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new AddInactiveBotsToRankedSearchCommand(), cancellationToken);
                if (result.IsFailed)
                {
                    logger.LogWarning("failed to add inactive bots to ranked search: {Reason}",
                        string.Join("\n", result.Errors.Select(error => error.Message)));
                }
            }

            await Task.Delay(options.Value.CheckIntervalMs, cancellationToken);
        }
    }
}