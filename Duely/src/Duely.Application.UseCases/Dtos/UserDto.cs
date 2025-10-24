using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class UserDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }
}
