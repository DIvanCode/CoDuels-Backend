using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.RankedSearches;

public sealed class RankedSearchCanceledDomainEvent : DomainEvent
{
    public RankedSearchCanceledDomainEvent(RankedSearchId id)
    {
        Id = id;
    }
    
    public RankedSearchId Id { get; init; }
}
