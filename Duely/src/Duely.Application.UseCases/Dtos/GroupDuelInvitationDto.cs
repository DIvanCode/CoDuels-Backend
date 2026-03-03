using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupDuelInvitationDto
{
    [JsonPropertyName("group")]
    public required GroupDto Group { get; init; }

    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
