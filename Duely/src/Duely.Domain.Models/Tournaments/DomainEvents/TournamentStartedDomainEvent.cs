using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Tournaments.Entities;

namespace Duely.Domain.Models.Tournaments.DomainEvents;

public sealed class TournamentStartedDomainEvent : DomainEvent
{
    public TournamentStartedDomainEvent(TournamentId id)
    {
        Id = id;
    }
    
    public TournamentId Id { get; init; }
}
