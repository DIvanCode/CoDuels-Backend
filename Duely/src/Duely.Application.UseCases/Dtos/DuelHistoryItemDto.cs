using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelHistoryItemDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("deadline_time")]
    public required DateTime DeadlineTime { get; init; }

    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; init; }
}
