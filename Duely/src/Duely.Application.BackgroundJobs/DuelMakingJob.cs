using Duely.Domain.Services.Duels;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob(IServiceProvider sp, IDuelManager duelManager, IOptions<DuelMakingJobOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var pair = duelManager.TryGetPair();

                if (pair is not null)
                {
                    var command = new CreateDuelCommand
                    {
                        User1Id = pair.Value.User1,
                        User2Id = pair.Value.User2
                    };

                    await mediator.Send(command, cancellationToken);
                }
            }

            await Task.Delay(options.Value.CheckPairIntervalMs, cancellationToken);
        }
    }
}
