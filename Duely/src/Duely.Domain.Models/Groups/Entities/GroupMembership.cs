// using System.ComponentModel;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Groups.Entities;
//
// public sealed class GroupMembership
// {
//     internal GroupMembership(Group group, User user, GroupRole role, bool isConfirmed)
//     {
//         Group = group;
//         User = user;
//         Role = role;
//         IsConfirmed = isConfirmed;
//     }
//     
//     public Group Group { get; init; }
//     public User User { get; init; }
//     public GroupRole Role { get; internal set; }
//     public bool IsConfirmed { get; internal set; }
//
//     // ReSharper disable once UnusedMember.Local
// #pragma warning disable CS8618, CS9264
//     /// <summary>
//     /// EF constructor. Do not use explicitly!
//     /// </summary>
//     [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
//     [EditorBrowsable(EditorBrowsableState.Never)]
//     private GroupMembership()
//     {
//     }
// #pragma warning restore CS8618, CS9264
// }
//
// public enum GroupRole
// {
//     Manager = 0,
//     Member = 1
// }
