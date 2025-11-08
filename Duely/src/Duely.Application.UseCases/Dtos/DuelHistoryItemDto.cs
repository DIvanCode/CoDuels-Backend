using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelHistoryItemDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }

    [JsonPropertyName("opponent_nickname")]
    public string OpponentNickname { get; init; } = null!;

    [JsonPropertyName("winner_nickname")]
    public string? WinnerNickname { get; init; }

    [JsonPropertyName("start_time")]
    public required DateTime StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; init; }
}
