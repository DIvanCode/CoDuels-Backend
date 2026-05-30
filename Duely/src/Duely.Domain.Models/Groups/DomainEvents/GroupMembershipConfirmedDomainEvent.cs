using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Groups.Entities;

namespace Duely.Domain.Models.Groups.DomainEvents;

public sealed class GroupMembershipConfirmedDomainEvent : DomainEvent
{
    public GroupMembershipConfirmedDomainEvent(GroupMembershipId id)
    {
        Id = id;
    }
    
    public GroupMembershipId Id { get; init; }
}
