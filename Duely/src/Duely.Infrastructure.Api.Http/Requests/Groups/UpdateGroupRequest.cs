using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.Groups;

public sealed class UpdateGroupRequest
{
    [JsonPropertyName("name"), Required]
    public required string Name { get; init; }
}
