using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob(IServiceProvider sp, IOptions<DuelMakingJobOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new TryCreateDuelCommand(), cancellationToken);
                if (result.IsFailed)
                {
                    Console.WriteLine(string.Join("\n", result.Errors.Select(e => e.Message)));
                }
            }

            await Task.Delay(options.Value.CheckPairIntervalMs, cancellationToken);
        }
    }
}
