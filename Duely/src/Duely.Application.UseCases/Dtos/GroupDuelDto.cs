using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class GroupDuelDto
{
    [JsonPropertyName("duel")]
    public DuelDto? Duel { get; init; }

    [JsonPropertyName("user1")]
    public required UserDto User1 { get; init; }

    [JsonPropertyName("user2")]
    public required UserDto User2 { get; init; }

    [JsonPropertyName("is_accepted_by_user1")]
    public required bool IsAcceptedByUser1 { get; init; }

    [JsonPropertyName("is_accepted_by_user2")]
    public required bool IsAcceptedByUser2 { get; init; }

    [JsonPropertyName("created_by")]
    public required UserDto CreatedBy { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("configuration_id")]
    public int? ConfigurationId { get; init; }
}
