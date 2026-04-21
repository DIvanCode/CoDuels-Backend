using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels;

namespace Duely.Infrastructure.Api.Http.Requests.CodeRuns;

public sealed class CreateCodeRunRequest
{
    [JsonPropertyName("duel_id"), Required]
    public required int DuelId { get; init; }

    [JsonPropertyName("task_key"), Required]
    public required char TaskKey { get; init; }

    [JsonPropertyName("time_limit"), Required]
    public required int TimeLimit { get; init; }

    [JsonPropertyName("memory_limit"), Required]
    public required int MemoryLimit { get; init; }

    [JsonPropertyName("code"), Required]
    public required string Code { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required Language Language { get; init; }

    [JsonPropertyName("input"), JsonRequired]
    public required string Input { get; init; }
}
