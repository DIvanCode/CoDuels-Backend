using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Duely.Application.Handlers.Duels.Notifications;

internal sealed class DuelParticipantReadyNotificationHandler(
    ILogger<DuelParticipantReadyNotificationHandler> logger,
    Context context)
    : INotificationHandler<DuelParticipantReadyDomainEvent>
{
    public async Task Handle(DuelParticipantReadyDomainEvent notification, CancellationToken cancellationToken)
    {
        var duel = notification.DuelParticipant.Duel;
        if (duel.Participants.Any(participant => !participant.IsReady))
        {
            return;
        }

        duel.SetReady();
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Duel {Id} is ready to start", duel.Id);
    }
}
