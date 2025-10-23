using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class TokenDto
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }
}
