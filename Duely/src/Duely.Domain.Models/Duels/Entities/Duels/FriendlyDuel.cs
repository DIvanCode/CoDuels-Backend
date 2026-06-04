using Duely.Domain.Models.Duels.DomainEvents.FriendlyDuels;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class FriendlyDuel : Duel
{
    public FriendlyDuel(
        DuelId id,
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
        if (IsConfirmed)
        {
            throw new InvalidOperationException("Нельзя заново потдвердить участие в дружеской дуэли.");
        }
        
        UpdatedAt = confirmedAt;
        IsConfirmed = true;
        
        AddDomainEvent(new FriendlyDuelConfirmedDomainEvent(Id));
    }

    public void Decline(DateTime declinedAt)
    {
        if (IsConfirmed)
        {
            throw new InvalidOperationException("Нельзя отклонить участие в ранее подтверждённой дружеской дуэли.");
        }
        
        UpdatedAt = declinedAt;
        IsConfirmed = false;
        
        AddDomainEvent(new FriendlyDuelDeclinedDomainEvent(Id));
    }

    public void Delete()
    {
        if (Status != DuelStatus.Pending)
        {
            throw new InvalidOperationException("Нельзя отменить начатую дружескую дуэль.");
        }
        
        AddDomainEvent(new FriendlyDuelDeletedDomainEvent(Id));
    }
}
