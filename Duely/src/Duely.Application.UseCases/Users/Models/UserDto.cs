using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Users.Models;

public sealed class UserDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("rating")]
    public required int Rating { get; init; }
}
