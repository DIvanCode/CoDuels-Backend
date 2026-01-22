using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public sealed class TestRequest
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    [JsonPropertyName("solution_id")]
    public required string SolutionId { get; init; }

    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }
}
