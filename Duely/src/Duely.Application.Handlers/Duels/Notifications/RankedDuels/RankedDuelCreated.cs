using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Duels.Notifications.RankedDuels;

internal sealed class RankedDuelCreatedNotificationHandler(Context context)
    : INotificationHandler<RankedDuelCreatedDomainEvent>
{
    public async Task Handle(RankedDuelCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        foreach (var participant in notification.RankedDuel.Participants)
        {
            var integrationEvent = new SendMessageIntegrationEvent(
                createdAt: DateTime.UtcNow, 
                attemptProcessAt: DateTime.UtcNow,
                userId: participant.User.Id,
                message: new RankedDuelCreatedMessage(notification.RankedDuel.Id),
                expirationTime: notification.RankedDuel.ConfirmTimeout);
            context.IntegrationEvents.Add(integrationEvent);
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }
}
