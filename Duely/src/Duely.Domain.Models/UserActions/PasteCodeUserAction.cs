using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duely.Domain.Models.UserActions;

public sealed class PasteCodeUserAction : UserAction
{
    [JsonIgnore, NotMapped]
    public override UserActionType Type => UserActionType.PasteCode;

    [JsonPropertyName("code_length"), Required]
    public required int CodeLength { get; init; }

    [JsonPropertyName("cursor_line"), Required]
    public required int CursorLine { get; init; }

    [JsonPropertyName("begin_line"), Required]
    public required int BeginLine { get; init; }

    [JsonPropertyName("end_line"), Required]
    public required int EndLine { get; init; }

    [JsonPropertyName("chars_count"), Required]
    public required int CharsCount { get; init; }
}
