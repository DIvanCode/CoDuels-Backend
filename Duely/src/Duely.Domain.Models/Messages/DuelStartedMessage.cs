using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class DuelStartedMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelStarted;

    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }
}
