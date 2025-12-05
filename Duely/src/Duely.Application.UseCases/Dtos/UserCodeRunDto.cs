using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class UserCodeRunDto
{
    [JsonPropertyName("run_id")]
    public int RunId { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("language")]
    public string Language { get; init; } = "";

    [JsonPropertyName("input")]
    public string Input { get; init; } = "";

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public UserCodeRunStatus Status { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
