using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Groups.Entities;

namespace Duely.Domain.Models.Groups.DomainEvents;

public sealed class GroupMembershipCreatedDomainEvent : DomainEvent
{
    public GroupMembershipCreatedDomainEvent(GroupMembershipId id)
    {
        Id = id;
    }
    
    public GroupMembershipId Id { get; init; }
}
