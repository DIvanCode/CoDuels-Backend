namespace Duely.Domain.Models.Groups;

public enum GroupRole
{
    Creator = 0,
    Manager = 1,
    Member = 2
}

public sealed class GroupMembership
{
    public required User User { get; init; }
    public required Group Group { get; init; }
    public required GroupRole Role { get; set; }
    public bool InvitationPending { get; set; }
    public User? InvitedBy { get; init; }
}
