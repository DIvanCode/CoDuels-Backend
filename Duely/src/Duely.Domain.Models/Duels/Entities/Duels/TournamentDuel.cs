using Duely.Domain.Models.Duels.DomainEvents.TournamentDuels;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class TournamentDuel : Duel
{
    public TournamentDuel(
        DuelId id,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt,
        Tournament tournament)
        : base(id, DuelType.TournamentDuel, configuration, participants, createdAt)
    {
        Tournament = tournament;
        
        foreach (var participant in participants)
        {
            _isConfirmed[participant.Id] = false;
        }
        
        AddDomainEvent(new TournamentDuelCreatedDomainEvent(Id));
    }
    
    public Tournament Tournament { get; init; }
    
    private readonly Dictionary<UserId, bool> _isConfirmed = [];
    public IReadOnlyDictionary<UserId, bool> IsConfirmed => _isConfirmed.AsReadOnly();
    
    public override void Start(DateTime startedAt, ProblemSet problemSet)
    {
        base.Start(startedAt, problemSet);
        
        AddDomainEvent(new TournamentDuelStartedDomainEvent(Id));
    }

    public override void Finish(DateTime finishedAt, User? winner)
    {
        base.Finish(finishedAt, winner);
        
        AddDomainEvent(new TournamentDuelFinishedDomainEvent(Id));
    }

    public void Confirm(DateTime confirmedAt, UserId userId)
    {
        UpdatedAt = confirmedAt;
        _isConfirmed[userId] = true;
        
        AddDomainEvent(new TournamentDuelConfirmedDomainEvent(Id, userId));
    }
}
