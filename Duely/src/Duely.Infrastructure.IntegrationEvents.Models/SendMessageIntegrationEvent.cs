using System.ComponentModel;
using Duely.Infrastructure.Gateway.Client.Abstracts;

namespace Duely.Infrastructure.IntegrationEvents.Models;

public sealed class SendMessageIntegrationEvent : IntegrationEvent
{
    public SendMessageIntegrationEvent(
        DateTime createdAt,
        DateTime attemptProcessAt,
        int userId,
        Message message,
        TimeSpan expirationTime)
        : base(IntegrationEventType.SendMessage, createdAt, attemptProcessAt)
    {
        UserId = userId;
        Message = message;
        ExpirationTime = expirationTime;
    }

    public int UserId { get; init; }
    public Message Message { get; init; }
    public TimeSpan ExpirationTime { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private SendMessageIntegrationEvent()
        : base(IntegrationEventType.SendMessage, default, default)
    {
    }
#pragma warning restore CS8618, CS9264
}
