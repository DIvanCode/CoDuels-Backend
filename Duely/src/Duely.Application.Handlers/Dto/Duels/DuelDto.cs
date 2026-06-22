// using System.Text.Json.Serialization;
// using Duely.Application.Handlers.Dto.Users;
// using Duely.Domain.Models.Duels.Entities;
//
// namespace Duely.Application.Handlers.Dto.Duels;
//
// public abstract class DuelDto
// {
//     [JsonPropertyName("id")]
//     public required Guid Id { get; init; }
//     
//     [JsonPropertyName("type")]
//     public required DuelType Type { get; init; }
//     
//     [JsonPropertyName("configuration")]
//     public required DuelConfigurationDto Configuration { get; init; }
//
//     [JsonPropertyName("participants")]
//     public required IReadOnlyCollection<UserShortDto> Participants { get; init; }
//
//     // [JsonPropertyName("problem_set")]
//     // public required ProblemSetDto? ProblemSet { get; init; }
//     
//     [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
//     public required DuelStatus Status { get; init; }
//     
//     [JsonPropertyName("created_at")]
//     public required DateTime CreatedAt { get; init; }
//     
//     [JsonPropertyName("started_at")]
//     public required DateTime? StartedAt { get; init; }
//     
//     [JsonPropertyName("finished_at")]
//     public required DateTime? FinishedAt { get; init; }
//     
//     [JsonPropertyName("winner")]
//     public required UserShortDto? Winner { get; init; }
// }
