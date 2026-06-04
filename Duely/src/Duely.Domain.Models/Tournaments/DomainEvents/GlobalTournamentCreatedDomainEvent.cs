using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Tournaments.Entities;

namespace Duely.Domain.Models.Tournaments.DomainEvents;

public sealed class GlobalTournamentCreatedDomainEvent : DomainEvent
{
    public GlobalTournamentCreatedDomainEvent(TournamentId id)
    {
        Id = id;
    }
    
    public TournamentId Id { get; init; }
}
