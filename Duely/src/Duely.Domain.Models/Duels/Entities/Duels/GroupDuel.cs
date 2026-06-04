using Duely.Domain.Models.Duels.DomainEvents.GroupDuels;
using Duely.Domain.Models.Duels.DomainEvents.GroupManualDuels;
using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class GroupDuel : Duel
{
    public GroupDuel(
        DuelId id,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt,
        Group group,
        User createdBy)
        : base(id, DuelType.GroupDuel, configuration, participants, createdAt)
    {
        Group = group;
        CreatedBy = createdBy;

        foreach (var participant in participants)
        {
            _isConfirmed[participant.Id] = false;
        }
        
        AddDomainEvent(new GroupDuelCreatedDomainEvent(Id));
    }

    public Group Group { get; init; }
    public User CreatedBy { get; init; }

    private readonly Dictionary<UserId, bool> _isConfirmed = [];
    public IReadOnlyDictionary<UserId, bool> IsConfirmed => _isConfirmed.AsReadOnly();

    public override void Start(DateTime startedAt, ProblemSet problemSet)
    {
        base.Start(startedAt, problemSet);
        
        AddDomainEvent(new GroupDuelStartedDomainEvent(Id));
    }

    public override void Finish(DateTime finishedAt, User? winner)
    {
        base.Finish(finishedAt, winner);
        
        AddDomainEvent(new GroupDuelFinishedDomainEvent(Id));
    }

    public void Confirm(DateTime confirmedAt, UserId userId)
    {
        if (!_isConfirmed.TryGetValue(userId, out var isConfirmed))
        {
            throw new InvalidOperationException("Пользователь не участвует в этой дуэли.");
        }
        
        if (isConfirmed)
        {
            throw new InvalidOperationException("Нельзя заново потдвердить участие в дуэли в группе.");
        }
        
        UpdatedAt = confirmedAt;
        _isConfirmed[userId] = true;
        
        AddDomainEvent(new GroupDuelConfirmedDomainEvent(Id, userId));
    }

    public void Decline(DateTime declinedAt, UserId userId)
    {
        if (!_isConfirmed.TryGetValue(userId, out var isConfirmed))
        {
            throw new InvalidOperationException("Пользователь не участвует в этой дуэли.");
        }
        
        if (isConfirmed)
        {
            throw new InvalidOperationException("Нельзя отклонить участие в ранее подтверждённой дуэли в группе.");
        }
        
        UpdatedAt = declinedAt;
        _isConfirmed[userId] = false;
        
        AddDomainEvent(new GroupDuelDeclinedDomainEvent(Id, userId));
    }
    
    public void Delete()
    {
        if (Status != DuelStatus.Pending)
        {
            throw new InvalidOperationException("Нельзя отменить начатую дуэль в группе.");
        }
        
        AddDomainEvent(new GroupDuelDeletedDomainEvent(Id));
    }
}
