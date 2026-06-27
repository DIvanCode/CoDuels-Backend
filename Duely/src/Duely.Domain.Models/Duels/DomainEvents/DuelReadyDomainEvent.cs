using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class DuelReadyDomainEvent(Duel duel) : DomainEvent
{
    public Duel Duel { get; init; } = duel;
}
