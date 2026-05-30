using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dto.Groups;

public sealed class GroupDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("memberships")]
    public required List<GroupMembershipDto> Memberships { get; init; }
}
