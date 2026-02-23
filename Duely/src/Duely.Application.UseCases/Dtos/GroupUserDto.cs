using System.Text.Json.Serialization;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupUserDto
{
    [JsonPropertyName("user")]
    public required UserDto User { get; init; }

    [JsonPropertyName("role"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupRole Role { get; init; }

    [JsonPropertyName("status"), JsonConverter(typeof(JsonStringEnumConverter))]
    public required GroupUserStatus Status { get; init; }

    [JsonPropertyName("invited_by")]
    public UserDto? InvitedBy { get; init; }
}

public enum GroupUserStatus
{
    Active = 0,
    Pending = 1
}
