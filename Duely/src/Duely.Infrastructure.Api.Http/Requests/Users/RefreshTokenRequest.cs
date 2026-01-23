using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Users;

public sealed class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }
}
