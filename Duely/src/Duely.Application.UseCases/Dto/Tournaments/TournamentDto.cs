using System.Text.Json.Serialization;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Models.Tournaments.Entities;

namespace Duely.Application.UseCases.Dto.Tournaments;

public abstract class TournamentDto
{
    [JsonPropertyName("id")]
    public required TournamentId Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("type"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required TournamentType Type { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required TournamentStatus Status { get; init; }

    [JsonPropertyName("created_by")]
    public required UserDto CreatedBy { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("participants")]
    public required IReadOnlyCollection<UserShortDto> Participants { get; init; }

    [JsonPropertyName("configuration")]
    public required TournamentConfigurationDto Configuration { get; init; }
}
