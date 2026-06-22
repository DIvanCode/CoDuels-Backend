using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.RankedSearches;

public sealed class RankedSearchStartedDomainEvent(Guid userId) : DomainEvent
{
    public Guid UserId { get; init; } = userId;
}
