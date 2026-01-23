using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class DuelInvitationCanceledMessage : Message
{
    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}
