using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels;

namespace Duely.Application.UseCases.Dtos;

public sealed class SingleEliminationBracketNodeDto
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("user")]
    public UserDto? User { get; init; }

    [JsonPropertyName("winner")]
    public UserDto? Winner { get; init; }

    [JsonPropertyName("duel_id")]
    public int? DuelId { get; init; }

    [JsonPropertyName("duel_status")]
    public DuelStatus? DuelStatus { get; init; }

    [JsonPropertyName("left_index")]
    public int? LeftIndex { get; init; }

    [JsonPropertyName("right_index")]
    public int? RightIndex { get; init; }
}
