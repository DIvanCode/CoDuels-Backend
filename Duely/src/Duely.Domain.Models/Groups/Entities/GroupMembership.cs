using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Groups.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Groups.Entities;

public sealed class GroupMembership : Entity<GroupMembershipId>
{
    public GroupMembership(
        GroupMembershipId id,
        User user,
        Group group,
        GroupRole role,
        bool isConfirmed) : base(id)
    {
        User = user;
        Group = group;
        Role = role;
        IsConfirmed = isConfirmed;
        
        AddDomainEvent(new GroupMembershipCreatedDomainEvent(Id));
    }
    
    public User User { get; init; }
    public Group Group { get; init; }
    public GroupRole Role { get; private set; }
    public bool IsConfirmed { get; private set; }

    public void ChangeRole(GroupRole role)
    {
        Role = role;
        
        AddDomainEvent(new GroupMembershipUpdatedDomainEvent(Id));
    }
    
    public void Confirm()
    {
        IsConfirmed = true;
        
        AddDomainEvent(new GroupMembershipConfirmedDomainEvent(Id));
    }
    
    public void Decline()
    {
        IsConfirmed = false;
        
        AddDomainEvent(new GroupMembershipDeclinedDomainEvent(Id));
    }

    public void Delete()
    {
        AddDomainEvent(new GroupMembershipDeletedDomainEvent(Id));
    }
}

public sealed record GroupMembershipId(Guid Value) : Identity<Guid>(Value);

public enum GroupRole
{
    Manager = 0,
    Member = 1
}
