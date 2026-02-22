using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public sealed class GroupInvitationMessage : Message
{
    [JsonPropertyName("group_id")]
    public required int GroupId { get; init; }

    [JsonPropertyName("group_name")]
    public required string GroupName { get; init; }

    [JsonPropertyName("role")]
    public required GroupRole Role { get; init; }

    [JsonPropertyName("invited_by")]
    public required string InvitedBy { get; init; }
}
