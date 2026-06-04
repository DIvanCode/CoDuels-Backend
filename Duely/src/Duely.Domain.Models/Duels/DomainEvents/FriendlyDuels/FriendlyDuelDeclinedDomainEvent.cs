using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.FriendlyDuels;

public sealed class FriendlyDuelDeclinedDomainEvent : DomainEvent
{
    public FriendlyDuelDeclinedDomainEvent(DuelId id)
    {
        Id = id;
    }
    
    public DuelId Id { get; init; }
}
