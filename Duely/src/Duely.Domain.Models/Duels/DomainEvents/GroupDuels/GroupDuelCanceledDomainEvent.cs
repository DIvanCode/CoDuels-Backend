using Duely.Domain.Kernel.DomainEvents;

namespace Duely.Domain.Models.Duels.DomainEvents.GroupDuels;

public sealed class GroupDuelCanceledDomainEvent(Guid id) : DomainEvent
{
    public Guid Id { get; init; } = id;
}
