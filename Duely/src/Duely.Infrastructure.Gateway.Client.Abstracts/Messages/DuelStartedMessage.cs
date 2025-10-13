using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public sealed class DuelStartedMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelStarted;

    [JsonPropertyName("duel_id")]
    public int DuelId { get; init; }
}
