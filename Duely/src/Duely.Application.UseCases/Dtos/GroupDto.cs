using System.Text.Json.Serialization;
using Duely.Domain.Models.Groups;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("user_role"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupRole UserRole { get; init; }
}
