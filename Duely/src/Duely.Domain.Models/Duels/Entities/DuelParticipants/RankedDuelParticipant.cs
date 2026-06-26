using System.ComponentModel;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities.DuelParticipants;

public sealed class RankedDuelParticipant : DuelParticipant
{
    public RankedDuelParticipant(User user, Duel duel) : base(DuelType.RankedDuel, user, duel)
    {
        InitialRating = user.Rating;
    }

    public int InitialRating { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private RankedDuelParticipant()
    {
    }
#pragma warning restore CS8618, CS9264
}
