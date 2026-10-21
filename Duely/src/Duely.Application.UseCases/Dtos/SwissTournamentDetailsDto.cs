using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class SwissTournamentDetailsDto
{
    [JsonPropertyName("standings")]
    public required List<SwissTournamentStandingDto> Standings { get; init; }
}

public sealed class SwissTournamentStandingDto
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }

    [JsonPropertyName("points")]
    public required int Points { get; init; }
}
