// using System.Text.Json.Serialization;
// using Duely.Domain.Models.Duels.Entities;
//
// namespace Duely.Application.Handlers.Dto.Duels;
//
// public sealed class DuelConfigurationDto
// {
//     [JsonPropertyName("id")]
//     public required Guid Id { get; init; }
//
//     [JsonPropertyName("should_show_opponent_solution")]
//     public required bool ShouldShowOpponentSolution { get; init; }
//
//     [JsonPropertyName("duration_minutes")]
//     public required int DurationMinutes { get; init; }
//
//     [JsonPropertyName("problems_count")]
//     public required int ProblemsCount { get; init; }
//
//     [JsonPropertyName("problems_order"), JsonConverter(typeof(JsonStringEnumConverter))]
//     public required ProblemsOrder ProblemsOrder { get; init; }
// }
