using FluentResults;
using MediatR;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest { }

public sealed class TryCreateDuelHandler(IDuelManager duelManager, IMediator mediator): IRequestHandler<TryCreateDuelCommand>
{
    public async Task Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
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
}