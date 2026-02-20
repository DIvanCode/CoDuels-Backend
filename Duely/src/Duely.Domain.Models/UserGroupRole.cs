namespace Duely.Domain.Models;

public enum GroupRole
{
    Creator = 0,
    Manager = 1,
    Member = 2
}

public sealed class UserGroupRole
{
    public required User User { get; init; }
    public required Group Group { get; init; }
    public required GroupRole Role { get; init; }
}
