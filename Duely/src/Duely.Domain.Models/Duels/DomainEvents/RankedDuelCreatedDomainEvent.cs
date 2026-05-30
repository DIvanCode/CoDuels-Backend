using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class RankedDuelCreatedDomainEvent : DomainEvent
{
    public RankedDuelCreatedDomainEvent(DuelId id)
    {
        Id = id;
    }
    
    public DuelId Id { get; init; }
}
