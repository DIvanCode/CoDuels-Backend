using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.TournamentDuels;

public sealed class TournamentDuelFinishedDomainEvent : DomainEvent
{
    public TournamentDuelFinishedDomainEvent(DuelId id)
    {
        Id = id;
    }
    
    public DuelId Id { get; init; }
}
