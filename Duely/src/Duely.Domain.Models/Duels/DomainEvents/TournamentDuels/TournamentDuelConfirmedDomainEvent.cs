using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.TournamentDuels;

public sealed class TournamentDuelConfirmedDomainEvent : DomainEvent
{
    public TournamentDuelConfirmedDomainEvent(DuelId id, UserId userId)
    {
        Id = id;
        UserId = userId;
    }
    
    public DuelId Id { get; init; }
    public UserId UserId { get; init; }
}
