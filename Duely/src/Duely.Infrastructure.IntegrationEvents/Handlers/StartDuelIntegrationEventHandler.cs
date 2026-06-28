using Duely.Application.Handlers.Duels.UseCases;
using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;
using MediatR;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class StartDuelIntegrationEventHandler(IMediator mediator)
    : IntegrationEventHandler<StartDuelIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.StartDuel;
    
    public override async Task<Result> Handle(
        StartDuelIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var command = new StartDuelCommand
        {
            DuelId = integrationEvent.DuelId,
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}
