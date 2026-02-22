using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Groups;

public sealed class ExcludeGroupUserRequest
{
    [JsonPropertyName("user_id"), Required]
    public required int UserId { get; init; }
}
