using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class OpponentSolutionUpdatedMessage : Message
{
    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }

    [JsonPropertyName("task_key")]
    public required string TaskKey { get; init; }

    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
