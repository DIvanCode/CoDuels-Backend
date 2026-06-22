using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Groups.DomainEvents;

public sealed class GroupMembershipUpdatedDomainEvent(Guid groupId, Guid userId) : DomainEvent
{
    public Guid GroupId { get; init; } = groupId;
    public Guid UserId { get; init; } = userId;
}
