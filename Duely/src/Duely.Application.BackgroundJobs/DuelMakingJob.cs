using Duely.Domain.Services;
using MediatR;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelMakingJob : BackgroundService
{
    private readonly DuelManager _duelManager;
    private readonly IMediator _mediator;

    public DuelMakingJob(DuelManager duelManager, IMediator mediator)
    {
        _duelManager = duelManager;
        _mediator = mediator;
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

            await Task.Delay(3000, cancellationToken);

        }
    }
}