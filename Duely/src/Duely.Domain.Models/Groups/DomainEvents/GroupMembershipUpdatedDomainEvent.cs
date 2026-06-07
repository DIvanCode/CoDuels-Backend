using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Groups.DomainEvents;

public sealed class GroupMembershipUpdatedDomainEvent : DomainEvent
{
    public GroupMembershipUpdatedDomainEvent(GroupId groupId, UserId userId)
    {
        GroupId = groupId;
        UserId = userId;
    }
    
    public GroupId GroupId { get; init; }
    public UserId UserId { get; init; }
}
