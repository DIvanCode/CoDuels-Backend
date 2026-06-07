using Duely.Domain.Models.Groups.Entities;

namespace Duely.Domain.Services.Groups;

public interface IGroupPermissionsService
{
    bool CanUpdateGroup(GroupMembership membership);
    bool CanCreateMembership(GroupMembership membership, GroupRole targetRole);
    bool CanUpdateMembership(GroupMembership membership, GroupMembership targetMembership);
    bool CanDeleteMembership(GroupMembership membership, GroupMembership targetMembership);
    bool CanCreateDuel(GroupMembership membership);
    bool CanDeleteDuel(GroupMembership membership);
    bool CanCreateTournament(GroupMembership membership);
    bool CanStartTournament(GroupMembership membership);
}

public sealed class GroupPermissionsService : IGroupPermissionsService
{
    public bool CanUpdateGroup(GroupMembership membership)
    {
        return membership is { IsConfirmed: true, Role: GroupRole.Manager };
    }
    
    public bool CanCreateMembership(GroupMembership membership, GroupRole targetRole)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role is GroupRole.Manager;
    }
    
    public bool CanUpdateMembership(GroupMembership membership, GroupMembership targetMembership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => targetMembership.Role is GroupRole.Member,
            _ => false
        };
    }

    public bool CanDeleteMembership(GroupMembership membership, GroupMembership targetMembership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => targetMembership.Role is GroupRole.Member,
            _ => false
        };
    }

    public bool CanCreateDuel(GroupMembership membership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => true,
            _ => false
        };
    }
    
    public bool CanDeleteDuel(GroupMembership membership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => true,
            _ => false
        };
    }
    
    public bool CanCreateTournament(GroupMembership membership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => true,
            _ => false
        };
    }
    
    public bool CanStartTournament(GroupMembership membership)
    {
        if (!membership.IsConfirmed)
        {
            return false;
        }

        return membership.Role switch
        {
            GroupRole.Manager => true,
            _ => false
        };
    }
}
