using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.DuelInvitations;

public sealed class GroupDuelInvitationRequest
{
    [JsonPropertyName("group_id"), Required]
    public required int GroupId { get; init; }

    [JsonPropertyName("user1_id"), Required]
    public required int User1Id { get; init; }

    [JsonPropertyName("user2_id"), Required]
    public required int User2Id { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}
