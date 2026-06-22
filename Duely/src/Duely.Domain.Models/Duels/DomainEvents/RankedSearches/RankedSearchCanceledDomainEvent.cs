using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.RankedSearches;

public sealed class RankedSearchCanceledDomainEvent(Guid userId) : DomainEvent
{
    public Guid UserId { get; init; } = userId;
}
