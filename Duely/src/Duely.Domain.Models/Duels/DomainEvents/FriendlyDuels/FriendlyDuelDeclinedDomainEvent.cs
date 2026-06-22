using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.FriendlyDuels;

public sealed class FriendlyDuelDeclinedDomainEvent(Guid id) : DomainEvent
{
    public Guid Id { get; init; } = id;
}
