using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Users.Requests;

public sealed class LoginRequest
{
    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }
}
