using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class SingleEliminationBracketDto
{
    [JsonPropertyName("nodes")]
    public required List<SingleEliminationBracketNodeDto?> Nodes { get; init; }
}
