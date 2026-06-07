using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Groups.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Groups.Entities;

public sealed class Group : Entity<GroupId>
{
    public Group(GroupId id, GroupName name) : base(id)
    {
        Name = name;
    }
    
    public GroupName Name { get; private set; }

    private readonly List<GroupMembership> _memberships = [];
    public IReadOnlyCollection<GroupMembership> Memberships => _memberships.AsReadOnly();

    public void UpdateName(GroupName name)
    {
        Name = name;
    }

    public GroupMembership? GetMembership(User user)
    {
        return _memberships.SingleOrDefault(m => m.User.Id == user.Id);
    }

    public GroupMembership CreateMembership(User user, GroupRole role, bool isConfirmed)
    {
        if (_memberships.Any(m => m.User.Id == user.Id))
        {
            throw new InvalidOperationException("Нельзя дважды добавить пользователя в группу.");
        }

        var membership = new GroupMembership(this, user, role, isConfirmed); 
        _memberships.Add(membership);
        
        AddDomainEvent(new GroupMembershipCreatedDomainEvent(Id, user.Id));
        
        return membership;
    }
    
    public void ConfirmMembership(User user)
    {
        var membership = _memberships.SingleOrDefault(m => m.User.Id == user.Id);

        if (membership is null)
        {
            throw new InvalidOperationException("Пользователь не приглашён в группу.");
        }
        
        if (membership.IsConfirmed)
        {
            throw new InvalidOperationException("Нельзя заново принять ранее принятое приглашение в группу.");
        }
        
        membership.IsConfirmed = true;
        
        AddDomainEvent(new GroupMembershipConfirmedDomainEvent(Id, user.Id));
    }
    
    public void DeclineMembership(User user)
    {
        var membership = _memberships.SingleOrDefault(m => m.User.Id == user.Id);

        if (membership is null)
        {
            throw new InvalidOperationException("Пользователь не приглашён в группу.");
        }
        
        if (membership.IsConfirmed)
        {
            throw new InvalidOperationException("Нельзя отлонить ранее принятое приглашение в группу.");
        }
        
        _memberships.Remove(membership);
        
        AddDomainEvent(new GroupMembershipDeclinedDomainEvent(Id, user.Id));
    }
    
    public void UpdateMembership(User user, GroupRole role)
    {
        var membership = _memberships.SingleOrDefault(m => m.User.Id == user.Id);

        if (membership is null)
        {
            throw new InvalidOperationException("Пользователь не в группе.");
        }
        
        membership.Role = role;
        
        AddDomainEvent(new GroupMembershipUpdatedDomainEvent(Id, user.Id));
    }

    public void DeleteMembership(User user)
    {
        var membership = _memberships.SingleOrDefault(m => m.User.Id == user.Id);

        if (membership is null)
        {
            throw new InvalidOperationException("Нельзя исключить пользователя из группе, в которой его нет.");
        }
        
        _memberships.Remove(membership);
        
        AddDomainEvent(new GroupMembershipDeletedDomainEvent(Id, user.Id));
    }
}

public sealed record GroupId(Guid Value) : Identity<Guid>(Value);
