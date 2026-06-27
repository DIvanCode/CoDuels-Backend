using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class DuelParticipantReadyDomainEvent(DuelParticipant duelParticipant) : DomainEvent
{
    public DuelParticipant DuelParticipant { get; init; } = duelParticipant;
}
