using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class DuelProblemVisibilityChangedDomainEvent(DuelProblem duelProblem) : DomainEvent
{
    public DuelProblem DuelProblem { get; init; } = duelProblem;
}
