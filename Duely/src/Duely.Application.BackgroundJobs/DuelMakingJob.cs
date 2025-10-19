using Duely.Domain.Services;
using Duely.Application.UseCases.CreateDuel;
using Duely.Application.Configuration;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob : BackgroundService
{
    private readonly IDuelManager _duelManager;
    private readonly DuelSettings _settings;
    private readonly IServiceProvider _sp;

    public DuelMakingJob(IServiceProvider sp, IDuelManager duelManager, IOptions<DuelSettings> options)
    {
        _sp = sp;
        _duelManager = duelManager;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            using (var scope = _sp.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var pair = _duelManager.TryGetPair();

                if (pair is not null) {
                    await mediator.Send(new CreateDuelCommand {
                        User1Id = pair.Value.User1,
                        User2Id = pair.Value.User2
                    }, cancellationToken);
                }

                await Task.Delay(_settings.CheckPairInterval, cancellationToken);
            }
            

        }
    }
}