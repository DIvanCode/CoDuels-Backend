using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using MediatR;

namespace Duely.Application.Handlers.Duels.Notifications.Submissions;

internal sealed class SubmissionCreatedDomainEventHandler(Context context)
    : INotificationHandler<SubmissionCreatedDomainEvent>
{
    public async Task Handle(SubmissionCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        
        
        await context.SaveChangesAsync(cancellationToken);
    }
}