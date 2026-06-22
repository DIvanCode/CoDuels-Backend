using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Users.Models;

public sealed class UserShortDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("nickname")]
    public required string Nickname { get; init; }
}
