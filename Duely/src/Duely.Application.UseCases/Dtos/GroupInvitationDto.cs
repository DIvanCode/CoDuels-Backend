using System.Text.Json.Serialization;
using Duely.Domain.Models.Groups;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupInvitationDto
{
    [JsonPropertyName("group_id")]
    public required int GroupId { get; init; }

    [JsonPropertyName("group_name")]
    public required string GroupName { get; init; }

    [JsonPropertyName("role"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupRole Role { get; init; }
}
