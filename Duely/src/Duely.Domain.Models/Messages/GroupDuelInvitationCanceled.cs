using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public class GroupDuelInvitationCanceledMessage : Message
{
    [JsonPropertyName("group_name")]
    public required string GroupName { get; init; }
    
    [JsonPropertyName("opponent_nickname")]
    public required string OpponentNickname { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}