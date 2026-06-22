using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class RankedDuel : Duel
{
    public RankedDuel(
        Guid id,
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt,
        IReadOnlyDictionary<Guid, int> initRatings)
        : base(id, DuelType.RankedDuel, configuration, participants, createdAt)
    {
        InitRatings = initRatings;
        
        AddDomainEvent(new RankedDuelCreatedDomainEvent(Id));
    }
    
    public IReadOnlyDictionary<Guid, int> InitRatings { get; init; }
    public IReadOnlyDictionary<Guid, int>? FinalRatings { get; private set; }

    // public void Finish(DateTime finishedAt, User? winner, IReadOnlyDictionary<Guid, int> finalRatings)
    // {
    //     FinalRatings = finalRatings;
    //     
    //     base.Finish(finishedAt, winner);
    // }
}
