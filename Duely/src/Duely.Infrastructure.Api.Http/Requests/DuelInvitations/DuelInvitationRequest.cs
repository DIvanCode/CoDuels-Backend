using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.DuelInvitations;

public sealed class DuelInvitationRequest
{
    [JsonPropertyName("opponent_nickname"), Required]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}
