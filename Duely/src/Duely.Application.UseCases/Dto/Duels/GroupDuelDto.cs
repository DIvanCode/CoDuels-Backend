using System.Text.Json.Serialization;
using Duely.Application.UseCases.Dto.Users;

namespace Duely.Application.UseCases.Dto.Duels;

public sealed class GroupDuelDto : DuelDto
{
    [JsonPropertyName("created_by")]
    public required UserShortDto CreatedBy { get; init; }

    [JsonPropertyName("is_confirmed")]
    public required IReadOnlyDictionary<Guid, bool> IsConfirmed { get; init; }
}
