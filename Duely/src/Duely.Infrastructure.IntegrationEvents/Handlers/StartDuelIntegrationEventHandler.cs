using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class StartDuelIntegrationEventHandler(
    ILogger<StartDuelIntegrationEventHandler> logger)
    : IntegrationEventHandler<StartDuelIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.StartDuel;
    
    public override async Task<Result> Handle(
        StartDuelIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Requested to start duel {Id}", integrationEvent.DuelId);
        return Result.Ok();
    }
}
