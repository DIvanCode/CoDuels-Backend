using Duely.Domain.Models;

namespace Duely.Domain.Services.Groups;

public interface IGroupPermissionsService
{
    bool HasReadPermission(GroupRole groupRole);
    bool HasUpdatePermission(GroupRole groupRole);
}

public sealed class GroupPermissionsService : IGroupPermissionsService
{
    public bool HasReadPermission(GroupRole groupRole)
    {
        return true;
    }

    public bool HasUpdatePermission(GroupRole groupRole)
    {
        return groupRole is GroupRole.Creator or GroupRole.Manager;
    }
}