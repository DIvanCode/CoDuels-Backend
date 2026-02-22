namespace Duely.Domain.Models;

public sealed class Group
{
    public int Id { get; init; }
    public required string Name { get; set; }

    public List<GroupMembership> Users { get; } = [];
}
