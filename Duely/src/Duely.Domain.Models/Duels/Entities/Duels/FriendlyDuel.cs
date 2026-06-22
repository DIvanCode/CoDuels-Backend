using Duely.Domain.Models.Duels.DomainEvents.FriendlyDuels;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class FriendlyDuel : Duel
{
    public FriendlyDuel(
        Guid id,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt,
        User createdBy)
        : base(id, DuelType.FriendlyDuel, configuration, participants, createdAt)
    {
        CreatedBy = createdBy;
        
        AddDomainEvent(new FriendlyDuelCreatedDomainEvent(Id));
    }

    public User CreatedBy { get; init; }
    public bool IsConfirmed { get; private set; }

    public void Confirm(DateTime confirmedAt)
    {
        UpdatedAt = confirmedAt;
        IsConfirmed = true;
        
        AddDomainEvent(new FriendlyDuelConfirmedDomainEvent(Id));
    }

    public void Decline(DateTime declinedAt)
    {
        UpdatedAt = declinedAt;
        IsConfirmed = false;
        
        AddDomainEvent(new FriendlyDuelDeclinedDomainEvent(Id));
    }

    public void Cancel()
    {
        AddDomainEvent(new FriendlyDuelCanceledDomainEvent(Id));
    }
}
