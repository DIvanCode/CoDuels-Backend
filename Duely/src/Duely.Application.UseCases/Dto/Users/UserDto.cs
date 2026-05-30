using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dto.Users;

public sealed class UserDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    [JsonPropertyName("rating")]
    public required int Rating { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
