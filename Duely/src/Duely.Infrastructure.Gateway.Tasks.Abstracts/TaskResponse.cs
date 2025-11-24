using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public sealed record TaskResponse
{
    [JsonPropertyName("id"), Required]
    public required string Id { get; init; }

    [JsonPropertyName("level"), Required]
    public required int Level { get; init; }
}
