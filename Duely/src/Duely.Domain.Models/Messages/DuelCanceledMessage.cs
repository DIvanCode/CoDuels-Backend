using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class DuelCanceledMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelCanceled;

    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }
}
