using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class DuelChangedMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelChanged;

    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }
}
