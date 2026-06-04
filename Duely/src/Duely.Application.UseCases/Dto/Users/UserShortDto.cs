using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dto.Users;

public sealed class UserShortDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }
}
