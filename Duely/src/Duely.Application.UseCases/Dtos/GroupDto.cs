using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
