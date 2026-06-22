// using Duely.Domain.Models.Duels.DomainEvents.GroupDuels;
// using Duely.Domain.Models.Groups.Entities;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Duels.Entities.Duels;
//
// public sealed class GroupDuel : Duel
// {
//     public GroupDuel(
//         Guid id,
//         DuelConfiguration configuration,
//         IReadOnlyCollection<User> participants,
//         DateTime createdAt,
//         Group group,
//         User createdBy)
//         : base(id, DuelType.GroupDuel, configuration, participants, createdAt)
//     {
//         Group = group;
//         CreatedBy = createdBy;
//
//         foreach (var participant in participants)
//         {
//             _isConfirmed[participant.Id] = false;
//         }
//         
//         AddDomainEvent(new GroupDuelCreatedDomainEvent(Id));
//     }
//
//     public Group Group { get; init; }
//     public User CreatedBy { get; init; }
//
//     private readonly Dictionary<Guid, bool> _isConfirmed = [];
//     public IReadOnlyDictionary<Guid, bool> IsConfirmed => _isConfirmed.AsReadOnly();
//
//     // public override void Start(DateTime startedAt, ProblemSet problemSet)
//     // {
//     //     base.Start(startedAt, problemSet);
//     //     
//     //     AddDomainEvent(new GroupDuelStartedDomainEvent(Id));
//     // }
//     //
//     // public override void Finish(DateTime finishedAt, User? winner)
//     // {
//     //     base.Finish(finishedAt, winner);
//     //     
//     //     AddDomainEvent(new GroupDuelFinishedDomainEvent(Id));
//     // }
//
//     public void Confirm(DateTime confirmedAt, Guid userId)
//     {
//         UpdatedAt = confirmedAt;
//         _isConfirmed[userId] = true;
//         
//         AddDomainEvent(new GroupDuelConfirmedDomainEvent(Id, userId));
//     }
//
//     public void Decline(DateTime declinedAt, Guid userId)
//     {
//         UpdatedAt = declinedAt;
//         _isConfirmed[userId] = false;
//         
//         AddDomainEvent(new GroupDuelDeclinedDomainEvent(Id, userId));
//     }
//     
//     public void Cancel()
//     {
//         AddDomainEvent(new GroupDuelCanceledDomainEvent(Id));
//     }
// }
