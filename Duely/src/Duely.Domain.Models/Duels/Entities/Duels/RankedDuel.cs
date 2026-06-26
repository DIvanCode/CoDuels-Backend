using System.ComponentModel;
using Duely.Domain.Models.Duels.DomainEvents.RankedDuels;
using Duely.Domain.Models.Duels.Entities.DuelParticipants;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.Duels;

public sealed class RankedDuel : Duel
{
    private RankedDuel(
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt)
        : base(DuelType.RankedDuel, configuration, createdAt)
    {
        _participants = participants
            .Select(p => new RankedDuelParticipant(p, this))
            .ToList();
    }
    
    private readonly List<RankedDuelParticipant> _participants;
    public IReadOnlyCollection<RankedDuelParticipant> Participants => _participants.AsReadOnly();

    public static RankedDuel Create(
        DuelConfiguration configuration,
        IReadOnlyCollection<User> participants,
        DateTime createdAt)
    {
        var rankedDuel = new RankedDuel(configuration, participants, createdAt);
        rankedDuel.AddDomainEvent(new RankedDuelCreatedDomainEvent(rankedDuel));
        return rankedDuel;
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
