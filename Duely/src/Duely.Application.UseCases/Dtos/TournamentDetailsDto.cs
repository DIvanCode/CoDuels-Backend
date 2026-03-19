using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class TournamentDetailsDto
{
    [JsonPropertyName("tournament")]
    public required TournamentDto Tournament { get; init; }

    [JsonPropertyName("single_elimination_bracket")]
    public SingleEliminationBracketDto? SingleEliminationBracket { get; init; }
}
