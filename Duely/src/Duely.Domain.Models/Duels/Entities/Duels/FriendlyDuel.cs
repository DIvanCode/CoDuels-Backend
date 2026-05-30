using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class FriendlyDuel : Duel
{
    public FriendlyDuel(
        DuelId id,
        DuelType type,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> users,
        ProblemSet problemSet,
        DateTime createdAt,
        User createdBy)
        : base(id, type, configuration, users, problemSet, createdAt)
    {
        CreatedBy = createdBy;
        
        AddDomainEvent(new FriendlyDuelCreatedDomainEvent(Id));
    }

    public User CreatedBy { get; init; }
    public bool IsConfirmed { get; private set; }
    
    public override void Start(DateTime startedAt)
    {
        base.Start(startedAt);
    }

    public void Finish(DateTime finishedAt, User? winner)
    {
        if (Status != DuelStatus.InProgress)
        {
            throw new InvalidOperationException("Нельзя закончить дуэль, которая не в процессе.");
        }
        
        Status = DuelStatus.Finished;
        FinishedAt = finishedAt;
        Winner = winner;
        
        AddDomainEvent(new DuelFinishedDomainEvent(Id));
    }

    public void Confirm()
    {
        IsConfirmed = true;
        
        AddDomainEvent(new FriendlyDuelConfirmedDomainEvent(Id));
    }

    public void Decline()
    {
        IsConfirmed = false;
        
        AddDomainEvent(new FriendlyDuelDeclinedDomainEvent(Id));
    }
}
