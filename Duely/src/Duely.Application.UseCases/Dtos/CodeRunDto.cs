using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class CodeRunDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required Language Language { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required UserCodeRunStatus Status { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
