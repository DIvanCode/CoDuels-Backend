using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Groups.Entities;

public sealed class Group : Entity<GroupId>
{
    public Group(GroupId id, GroupName name) : base(id)
    {
        Name = name;
    }
    
    public GroupName Name { get; private set; }

    public IReadOnlyCollection<GroupMembership> Memberships { get; init; } = [];

    public void UpdateName(GroupName name)
    {
        Name = name;
    }
}

public sealed record GroupId(Guid Value) : Identity<Guid>(Value);
