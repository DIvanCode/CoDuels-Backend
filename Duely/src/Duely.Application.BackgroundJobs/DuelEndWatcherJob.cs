using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelEndWatcherJob(IServiceProvider sp, IOptions<DuelEndWatcherJobOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
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
