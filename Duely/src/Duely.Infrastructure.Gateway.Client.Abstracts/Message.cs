using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RankedDuelCreatedMessage), nameof(MessageType.RankedDuelCreated))]
public abstract class Message
{
    protected Message()
    {
    }
}

public enum MessageType
{
    RankedDuelCreated = 0
}
