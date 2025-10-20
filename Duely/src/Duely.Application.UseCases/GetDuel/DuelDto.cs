using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.GetDuel;

public sealed class DuelDto 
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    [JsonPropertyName("user1_id")]
    public required int User1Id { get; init; }

    [JsonPropertyName("user2_id")]
    public required int User2Id { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("result"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelResult Result { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public required DateTime? EndTime { get; init; }

    [JsonPropertyName("max_duration")]
    public required int MaxDuration { get; init; }
}