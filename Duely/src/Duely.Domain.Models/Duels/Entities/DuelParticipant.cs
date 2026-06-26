using System.ComponentModel;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public abstract class DuelParticipant
{
    protected DuelParticipant(DuelType type, User user, Duel duel)
    {
        Type = type;
        User = user;
        Duel = duel;
    }

    public DuelType Type { get; init; }
    public User User { get; init; }
    public Duel Duel { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected DuelParticipant()
    {
    }
#pragma warning restore CS8618, CS9264
}
