using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class UserCreatedIntegrationEventHandler
    : IntegrationEventHandler<UserCreatedIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.UserCreated;
    
    public override async Task<Result> Handle(
        UserCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        return new IntegrationEventExpiredError();
    }
}
