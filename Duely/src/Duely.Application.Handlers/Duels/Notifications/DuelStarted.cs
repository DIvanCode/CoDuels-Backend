using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Duels.Notifications;

internal sealed class DuelStartedNotificationHandler(Context context)
    : INotificationHandler<DuelStartedDomainEvent>
{
    public async Task Handle(DuelStartedDomainEvent notification, CancellationToken cancellationToken)
    {
        foreach (var participant in notification.Duel.Participants)
        {
            var integrationEvent = new SendMessageIntegrationEvent(
                createdAt: DateTime.UtcNow, 
                attemptProcessAt: DateTime.UtcNow,
                userId: participant.User.Id,
                message: new DuelStartedMessage(notification.Duel.Id),
                expirationTime: TimeSpan.FromMinutes(notification.Duel.Configuration.DurationMinutes));
            context.IntegrationEvents.Add(integrationEvent);
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }
}
