using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.TournamentDuels;

public sealed class TournamentDuelStartedDomainEvent : DomainEvent
{
    public TournamentDuelStartedDomainEvent(DuelId id)
    {
        Id = id;
    }
    
    public DuelId Id { get; init; }
}
