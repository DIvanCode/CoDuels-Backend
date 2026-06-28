using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RankedDuelCreatedMessage), nameof(MessageType.RankedDuelCreated))]
[JsonDerivedType(typeof(DuelStartedMessage), nameof(MessageType.DuelStarted))]
public abstract class Message
{
    protected Message()
    {
    }
}

public enum MessageType
{
    RankedDuelCreated = 0,
    DuelStarted = 1,
}
