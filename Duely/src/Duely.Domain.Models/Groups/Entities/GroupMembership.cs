using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Groups.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Groups.Entities;

public sealed class GroupMembership : ValueObject
{
    internal GroupMembership(Group group, User user, GroupRole role, bool isConfirmed)
    {
        Group = group;
        User = user;
        Role = role;
        IsConfirmed = isConfirmed;
    }
    
    public Group Group { get; init; }
    public User User { get; init; }
    public GroupRole Role { get; internal set; }
    public bool IsConfirmed { get; internal set; }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return User.Id;
        yield return Group.Id;
    }
}

public enum GroupRole
{
    Manager = 0,
    Member = 1
}
