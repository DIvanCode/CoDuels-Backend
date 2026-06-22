using Duely.Domain.Models.Users.DomainEvents;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Users.Notifications;

internal sealed class UserCreatedNotificationHandler(Context context)
    : INotificationHandler<UserCreatedDomainEvent>
{
    public async Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new UserCreatedIntegrationEvent(
            createdAt: DateTime.UtcNow, 
            attemptProcessAt: DateTime.UtcNow, 
            userId: notification.User.Id);
        
        context.IntegrationEvents.Add(integrationEvent);
        await context.SaveChangesAsync(cancellationToken);
    }
}
