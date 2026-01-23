using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Api.Http.Requests.CodeRuns;

public sealed class CreateCodeRunRequest
{
    [JsonPropertyName("code"), Required]
    public required string Code { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required Language Language { get; init; }

    [JsonPropertyName("input"), Required]
    public required string Input { get; init; }
}
