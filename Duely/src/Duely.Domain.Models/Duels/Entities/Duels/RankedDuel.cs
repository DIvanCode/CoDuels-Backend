using System.ComponentModel;
using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Domain.Models.Duels.Entities.DuelParticipants;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class RankedDuel : Duel
{
    private RankedDuel(DuelConfiguration configuration)
        : base(DuelType.Ranked, configuration)
    {
    }

    public static RankedDuel Create(DuelConfiguration configuration)
    {
        var rankedDuel = new RankedDuel(configuration);
        rankedDuel.AddDomainEvent(new RankedDuelCreatedDomainEvent(rankedDuel));
        return rankedDuel;
    }

    public override void AddParticipant(User user)
    {
        AddParticipant(new RankedDuelParticipant(user, this));
    }

    // public void Finish(DateTime finishedAt, User? winner, IReadOnlyDictionary<Guid, int> finalRatings)
    // {
    //     FinalRatings = finalRatings;
    //     
    //     base.Finish(finishedAt, winner);
    // }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private RankedDuel()
    {
    }
#pragma warning restore CS8618, CS9264
}
