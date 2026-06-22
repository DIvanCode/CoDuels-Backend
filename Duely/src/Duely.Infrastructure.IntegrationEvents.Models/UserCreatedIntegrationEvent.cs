using System.ComponentModel;

namespace Duely.Infrastructure.IntegrationEvents.Models;

public sealed class UserCreatedIntegrationEvent : IntegrationEvent
{
    public UserCreatedIntegrationEvent(DateTime createdAt, DateTime attemptProcessAt, int userId)
        : base(IntegrationEventType.UserCreated, createdAt, attemptProcessAt)
    {
        UserId = userId;
    }

    public int UserId { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private UserCreatedIntegrationEvent()
        : base(IntegrationEventType.UserCreated, default, default)
    {
    }
#pragma warning restore CS8618, CS9264
}
