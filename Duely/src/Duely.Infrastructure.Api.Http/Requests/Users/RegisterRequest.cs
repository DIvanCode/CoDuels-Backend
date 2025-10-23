using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Users;

public sealed class RegisterRequest
{
    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }
}
