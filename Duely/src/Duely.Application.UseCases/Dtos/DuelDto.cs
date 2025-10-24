using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public enum DuelResult
{
    Win = 0,
    Lose = 1,
    Draw = 2
}

public sealed class DuelDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    [JsonPropertyName("opponent_id")]
    public required int OpponentId { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("deadline_time")]
    public required DateTime DeadlineTime { get; init; }

    [JsonPropertyName("result"), JsonConverter(typeof(JsonStringEnumConverter))]
    public DuelResult? Result { get; set; }

    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; set; }
}
