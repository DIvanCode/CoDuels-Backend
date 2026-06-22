// using Duely.Domain.Kernel.Entities;
// using Duely.Domain.Models.Duels.DomainEvents.RankedSearches;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Duels.Entities;
//
// public sealed class RankedSearch : Entity
// {
//     public RankedSearch(User user, int rating, DateTime startedAt)
//     {
//         User = user;
//         StartedAt = startedAt;
//         Rating = rating;
//         Seed = Random.Shared.Next();
//         
//         AddDomainEvent(new RankedSearchStartedDomainEvent(User.Id));
//     }
//     
//     public User User { get; init; }
//     public DateTime StartedAt { get; init; }
//     public int Rating { get; init; }
//     public int Seed { get; init; }
//
//     public void Cancel()
//     {
//         AddDomainEvent(new RankedSearchCanceledDomainEvent(User.Id));
//     }
// }
