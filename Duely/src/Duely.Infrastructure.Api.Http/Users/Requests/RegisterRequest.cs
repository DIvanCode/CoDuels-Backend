using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Users.Requests;

internal sealed class RegisterRequest
{
    [JsonPropertyName("nickname"), Required]
    public required string Nickname { get; init; }

    [JsonPropertyName("password"), Required]
    public required string Password { get; init; }
}
