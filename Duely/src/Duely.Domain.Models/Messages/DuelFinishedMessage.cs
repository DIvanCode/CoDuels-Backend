using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class DuelFinishedMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelFinished;

    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }
}
