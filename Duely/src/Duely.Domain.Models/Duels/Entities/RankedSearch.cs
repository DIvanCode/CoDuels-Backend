using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Duels.DomainEvents.RankedSearches;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public sealed class RankedSearch : Entity<RankedSearchId>
{
    public RankedSearch(RankedSearchId id, User user, DateTime startedAt) : base(id)
    {
        User = user;
        StartedAt = startedAt;
        Seed = Random.Shared.Next();
        
        AddDomainEvent(new RankedSearchStartedDomainEvent(Id));
    }
    
    public User User { get; init; }
    public DateTime StartedAt { get; init; }
    public int Seed { get; init; }

    public void Cancel()
    {
        AddDomainEvent(new RankedSearchCanceledDomainEvent(Id));
    }
}

public sealed record RankedSearchId(Guid Value) : Identity<Guid>(Value);
