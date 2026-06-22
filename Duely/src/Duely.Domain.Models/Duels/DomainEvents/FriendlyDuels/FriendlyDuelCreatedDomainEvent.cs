using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.FriendlyDuels;

public sealed class FriendlyDuelCreatedDomainEvent(Guid id) : DomainEvent
{
    public Guid Id { get; init; } = id;
}
