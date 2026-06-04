using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.GroupDuels;

public sealed class GroupDuelDeletedDomainEvent : DomainEvent
{
    public GroupDuelDeletedDomainEvent(DuelId id)
    {
        Id = id;
    }
    
    public DuelId Id { get; init; }
}
