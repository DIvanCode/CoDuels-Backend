using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents.GroupManualDuels;

public sealed class GroupDuelDeclinedDomainEvent : DomainEvent
{
    public GroupDuelDeclinedDomainEvent(DuelId id, UserId userId)
    {
        Id = id;
        UserId = userId;
    }
    
    public DuelId Id { get; init; }
    public UserId UserId { get; init; }
}
