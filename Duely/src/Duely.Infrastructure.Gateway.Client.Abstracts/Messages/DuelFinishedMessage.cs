using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Client.Abstracts.Messages;

public sealed class DuelFinishedMessage : Message
{
    [JsonIgnore]
    public override MessageType Type => MessageType.DuelFinished;
    [JsonIgnore]
    public int DuelId { get; init; }
    [JsonPropertyName("winner")]
    public string Winner { get; init; } = string.Empty;
}
