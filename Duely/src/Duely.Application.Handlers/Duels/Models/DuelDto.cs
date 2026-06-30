using System.Text.Json.Serialization;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Application.Handlers.Duels.Models;

public sealed class DuelDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("type"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelType Type { get; init; }

    [JsonPropertyName("configuration")]
    public required DuelConfigurationDto Configuration { get; init; }

    [JsonPropertyName("participants")]
    public required IReadOnlyCollection<DuelParticipantDto> Participants { get; init; }
    
    [JsonPropertyName("problems")]
    public required IReadOnlyCollection<DuelProblemDto> Problems { get; init; }
    
    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required DuelStatus Status { get; init; }
    
    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
    
    [JsonPropertyName("started_at")]
    public required DateTime? StartedAt { get; init; }
}
