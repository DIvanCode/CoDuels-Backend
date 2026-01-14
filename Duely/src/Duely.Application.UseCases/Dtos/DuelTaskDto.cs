using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class DuelTaskDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
