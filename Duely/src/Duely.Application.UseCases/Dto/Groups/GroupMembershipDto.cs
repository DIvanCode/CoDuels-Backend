using System.Text.Json.Serialization;
using Duely.Application.UseCases.Dto.Users;
using Duely.Domain.Models.Groups.Entities;

namespace Duely.Application.UseCases.Dto.Groups;

public sealed class GroupMembershipDto
{
    [JsonPropertyName("user")]
    public required UserShortDto User { get; init; }
    
    [JsonPropertyName("role"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupRole Role { get; init; }
    
    [JsonPropertyName("is_confirmed")]
    public required bool IsConfirmed { get; init; }
}
