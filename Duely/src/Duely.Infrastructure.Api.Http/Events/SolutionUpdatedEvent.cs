using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Api.Http.Events;

public sealed class SolutionUpdatedEvent : Event
{
    [JsonPropertyName("duel_id")]
    public required int DuelId { get; init; }

    [JsonPropertyName("task_key")]
    public required string TaskKey { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }

    [JsonPropertyName("solution")]
    public required string Solution { get; init; }
}
