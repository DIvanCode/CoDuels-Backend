using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Groups.Requests;

public sealed class DeleteGroupMembershipRequest
{
    [JsonPropertyName("user_id"), Required]
    public required Guid UserId { get; init; }
}
