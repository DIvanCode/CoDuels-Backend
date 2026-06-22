using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.RankedDuels;

public sealed class RankedDuelCreatedDomainEvent(Guid id) : DomainEvent
{
    public Guid Id { get; init; } = id;
}
