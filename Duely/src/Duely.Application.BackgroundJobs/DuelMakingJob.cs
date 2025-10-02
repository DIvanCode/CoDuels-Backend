using Duely.Domain.Services;
using Duely.Application.UseCases.CreateDuel;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob : BackgroundService
{
    private readonly DuelManager _duelManager;
    private readonly IMediator _mediator;
    private readonly DuelSettings _settings;

    public DuelMakingJob(IDuelManager duelManager, IMediator mediator, IOptions<DuelSettings> options)
    {
        _duelManager = duelManager;
        _mediator = mediator;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            var pair = _duelManager.TryGetPair();

            if (pair is not null) {
                await _mediator.Send(new CreateDuelCommand {
                    User1Id = pair.Value.User1,
                    User2Id = pair.Value.User2
                }, cancellationToken);
            }

            await Task.Delay(_settings.CheckPairInterval, cancellationToken);

        }
    }
}