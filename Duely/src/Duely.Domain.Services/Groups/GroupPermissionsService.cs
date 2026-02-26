using Duely.Domain.Models.Groups;

namespace Duely.Domain.Services.Groups;

public interface IGroupPermissionsService
{
    bool CanViewGroup(GroupMembership membership);
    bool CanUpdateGroup(GroupMembership membership);
    bool CanAssignRole(GroupMembership actor, GroupRole targetRole);
    bool CanChangeRole(GroupMembership actor, GroupMembership target, GroupRole targetRole);
    bool CanExclude(GroupMembership actor, GroupRole targetRole);
    bool CanLeave(GroupMembership membership);
}

public sealed class GroupPermissionsService : IGroupPermissionsService
{
    public bool CanViewGroup(GroupMembership membership)
    {
        return membership.InvitationPending is false;
    }

    public bool CanUpdateGroup(GroupMembership membership)
    {
        return membership is { InvitationPending: false, Role: GroupRole.Creator or GroupRole.Manager };
    }

    public bool CanAssignRole(GroupMembership actor, GroupRole targetRole)
    {
        if (actor.InvitationPending)
        {
            return false;
        }

        return actor.Role switch
        {
            GroupRole.Creator => targetRole is GroupRole.Manager or GroupRole.Member,
            GroupRole.Manager => targetRole is GroupRole.Manager or GroupRole.Member,
            _ => false
        };
    }
    
    public bool CanChangeRole(GroupMembership actor, GroupMembership target, GroupRole targetRole)
    {
        if (actor.InvitationPending)
        {
            return false;
        }

        return actor.Role switch
        {
            GroupRole.Creator => target.Role is GroupRole.Manager or GroupRole.Member && 
                                 targetRole is GroupRole.Manager or GroupRole.Member,
            GroupRole.Manager => target.Role is GroupRole.Member &&
                                 targetRole is GroupRole.Manager or GroupRole.Member,
            _ => false
        };
    }

    public bool CanExclude(GroupMembership actor, GroupRole targetRole)
    {
        if (actor.InvitationPending)
        {
            return false;
        }

        return actor.Role switch
        {
            GroupRole.Creator => targetRole is GroupRole.Manager or GroupRole.Member,
            GroupRole.Manager => targetRole is GroupRole.Member,
            _ => false
        };
    }

    public bool CanLeave(GroupMembership membership)
    {
        return membership.InvitationPending is false;
    }
}
