using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Duels.Notifications;

internal sealed class DuelReadyNotificationHandler(Context context)
    : INotificationHandler<DuelReadyDomainEvent>
{
    public async Task Handle(DuelReadyDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new StartDuelIntegrationEvent(
            createdAt: DateTime.UtcNow,
            attemptProcessAt: DateTime.UtcNow,
            duelId: notification.Duel.Id);
        
        context.Add(integrationEvent);
        await context.SaveChangesAsync(cancellationToken);
    }
}
