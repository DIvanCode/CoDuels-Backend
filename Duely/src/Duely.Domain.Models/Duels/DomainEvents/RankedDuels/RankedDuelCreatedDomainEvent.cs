using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Duels.Entities.Duels;

namespace Duely.Domain.Models.Duels.DomainEvents.RankedDuels;

public sealed class RankedDuelCreatedDomainEvent(RankedDuel rankedDuel) : DomainEvent
{
    public RankedDuel RankedDuel { get; init; } = rankedDuel;
}
