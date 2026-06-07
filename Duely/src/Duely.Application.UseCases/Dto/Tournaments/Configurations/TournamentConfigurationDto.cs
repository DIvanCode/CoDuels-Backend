using System.Text.Json.Serialization;
using Duely.Application.UseCases.Dto.Duels;
using Duely.Domain.Models.Tournaments.Entities;

namespace Duely.Application.UseCases.Dto.Tournaments.Configurations;

public abstract class TournamentConfigurationDto
{
    [JsonPropertyName("type"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required TournamentConfigurationType Type { get; init; }
    
    [JsonPropertyName("duel_configuration")]
    public required DuelConfigurationDto DuelConfiguration { get; init; }
}
