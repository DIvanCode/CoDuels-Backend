using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Infrastructure.Api.Http.Requests.Groups;

public sealed class InviteGroupUserRequest
{
    [JsonPropertyName("group_id"), Required]
    public required int GroupId { get; init; }
    
    [JsonPropertyName("user_id"), Required]
    public required int UserId { get; init; }

    [JsonPropertyName("role"), Required, JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupRole Role { get; init; }
}
