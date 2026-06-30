using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Duels.Notifications;

internal sealed class DuelCanceledNotificationHandler(Context context)
    : INotificationHandler<DuelCanceledDomainEvent>
{
    private static readonly TimeSpan ExpirationTime = TimeSpan.FromMinutes(5);
    
    public async Task Handle(DuelCanceledDomainEvent notification, CancellationToken cancellationToken)
    {
        context.Duels.Remove(notification.Duel);
        
        foreach (var participant in notification.Duel.Participants)
        {
            var integrationEvent = new SendMessageIntegrationEvent(
                createdAt: DateTime.UtcNow, 
                attemptProcessAt: DateTime.UtcNow,
                userId: participant.User.Id,
                message: new DuelCanceledMessage(notification.Duel.Id),
                expirationTime: ExpirationTime);
            context.IntegrationEvents.Add(integrationEvent);
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }
}
