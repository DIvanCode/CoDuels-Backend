using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class RankedDuel : Duel
{
    public RankedDuel(
        DuelId id,
        DuelType type,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        ProblemSet problemSet,
        DateTime createdAt,
        IReadOnlyDictionary<UserId, Rating> initRatings)
        : base(id, type, configuration, participants, problemSet, createdAt)
    {
        InitRatings = initRatings;
        
        AddDomainEvent(new RankedDuelCreatedDomainEvent(Id));
    }
    
    public IReadOnlyDictionary<UserId, Rating> InitRatings { get; init; }
    public IReadOnlyDictionary<UserId, Rating>? FinalRatings { get; init; }
    
    public void Finish()
}