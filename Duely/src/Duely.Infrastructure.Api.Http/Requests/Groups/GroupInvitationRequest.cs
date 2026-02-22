using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Groups;

public sealed class GroupInvitationRequest
{
    [JsonPropertyName("group_id"), Required]
    public required int GroupId { get; init; }
}
