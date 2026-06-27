using System.ComponentModel;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.Gateway.Client.Abstracts;

namespace Duely.Infrastructure.IntegrationEvents.Models;

public sealed class StartDuelIntegrationEvent : IntegrationEvent
{
    public StartDuelIntegrationEvent(
        DateTime createdAt,
        DateTime attemptProcessAt,
        int duelId)
        : base(IntegrationEventType.StartDuel, createdAt, attemptProcessAt)
    {
        DuelId = duelId;
    }

    public int DuelId { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private StartDuelIntegrationEvent()
        : base(IntegrationEventType.StartDuel, default, default)
    {
    }
#pragma warning restore CS8618, CS9264
}
