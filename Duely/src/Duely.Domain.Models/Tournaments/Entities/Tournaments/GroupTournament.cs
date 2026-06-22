// using Duely.Domain.Models.Groups.Entities;
// using Duely.Domain.Models.Tournaments.DomainEvents;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Tournaments.Entities.Tournaments;
//
// public sealed class GroupTournament : Tournament
// {
//     public GroupTournament(
//         TournamentId id,
//         TournamentName name,
//         User createdBy,
//         DateTime createdAt,
//         TournamentConfiguration configuration,
//         Group group)
//         : base(id, name, TournamentType.Group, createdBy, createdAt, configuration)
//     {
//         Group = group;
//         
//         AddDomainEvent(new GroupTournamentCreatedDomainEvent(Id));
//     }
//
//     public Group Group { get; init; }
//
//     public override void Start()
//     {
//         base.Start();
//         
//         AddDomainEvent(new GroupTournamentStartedDomainEvent(Id));
//     }
// }
