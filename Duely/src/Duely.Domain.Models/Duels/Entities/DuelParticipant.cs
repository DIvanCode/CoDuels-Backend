using System.ComponentModel;
using Duely.Domain.Kernel.Entities;
using Duely.Domain.Models.Duels.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Duels.Entities;

public abstract class DuelParticipant : Entity
{
    protected DuelParticipant(User user, Duel duel)
    {
        Type = duel.Type;
        User = user;
        Duel = duel;
    }

    public DuelType Type { get; init; }
    public User User { get; init; }
    public Duel Duel { get; init; }
    public bool IsReady { get; private set; }

    public void SetReady()
    {
        IsReady = true;

        AddDomainEvent(new DuelParticipantReadyDomainEvent(this));
    }
    
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
