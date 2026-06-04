using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class RankedDuel : Duel
{
    public RankedDuel(
        DuelId id,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt,
        IReadOnlyDictionary<UserId, Rating> initRatings)
        : base(id, DuelType.RankedDuel, configuration, participants, createdAt)
    {
        InitRatings = initRatings;
        
        AddDomainEvent(new RankedDuelCreatedDomainEvent(Id));
    }
    
    public IReadOnlyDictionary<UserId, Rating> InitRatings { get; init; }
    public IReadOnlyDictionary<UserId, Rating>? FinalRatings { get; private set; }

    public void Finish(DateTime finishedAt, User? winner, IReadOnlyDictionary<UserId, Rating> finalRatings)
    {
        FinalRatings = finalRatings;
        
        base.Finish(finishedAt, winner);
    }
}
