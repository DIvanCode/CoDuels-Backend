// using Duely.Domain.Kernel.Entities;
// using Duely.Domain.Models.Groups.DomainEvents;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Groups.Entities;
//
// public sealed class Group : Entity
// {
//     public Group(Guid id, string name)
//     {
//         Id = id;
//         Name = name;
//     }
//
//     public Guid Id { get; init; }
//     public string Name { get; private set; }
//
//     private readonly List<GroupMembership> _memberships = [];
//     public IReadOnlyCollection<GroupMembership> Memberships => _memberships.AsReadOnly();
//
//     public void UpdateName(string name)
//     {
//         Name = name;
//     }
//
//     public GroupMembership? GetMembership(User user)
//     {
//         return _memberships.SingleOrDefault(m => m.User.Id == user.Id);
//     }
//
//     public GroupMembership CreateMembership(User user, GroupRole role, bool isConfirmed)
//     {
//         var membership = new GroupMembership(this, user, role, isConfirmed); 
//         _memberships.Add(membership);
//         
//         AddDomainEvent(new GroupMembershipCreatedDomainEvent(Id, user.Id));
//         
//         return membership;
//     }
//     
//     public void ConfirmMembership(GroupMembership membership)
//     {
//         membership.IsConfirmed = true;
//         
//         AddDomainEvent(new GroupMembershipConfirmedDomainEvent(Id, membership.User.Id));
//     }
//     
//     public void DeclineMembership(GroupMembership membership)
//     {
//         _memberships.Remove(membership);
//         
//         AddDomainEvent(new GroupMembershipDeclinedDomainEvent(Id, membership.User.Id));
//     }
//     
//     public void UpdateMembership(GroupMembership membership, GroupRole role)
//     {
//         membership.Role = role;
//         
//         AddDomainEvent(new GroupMembershipUpdatedDomainEvent(Id, membership.User.Id));
//     }
//
//     public void DeleteMembership(GroupMembership membership)
//     {
//         _memberships.Remove(membership);
//         
//         AddDomainEvent(new GroupMembershipDeletedDomainEvent(Id, membership.User.Id));
//     }
// }
