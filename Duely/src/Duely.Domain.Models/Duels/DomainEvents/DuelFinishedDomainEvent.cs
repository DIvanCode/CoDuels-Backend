using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class DuelFinishedDomainEvent(Guid id) : DomainEvent
{
    public Guid Id { get; init; } = id;
}
