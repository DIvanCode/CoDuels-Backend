using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duely.Domain.Models.UserActions;

public sealed class MoveCursorUserAction : UserAction
{
    [JsonIgnore, NotMapped]
    public override UserActionType Type => UserActionType.MoveCursor;

    [JsonPropertyName("code_length"), Required]
    public required int CodeLength { get; init; }

    [JsonPropertyName("cursor_line"), Required]
    public required int CursorLine { get; init; }

    [JsonPropertyName("previous_cursor_line"), Required]
    public required int PreviousCursorLine { get; init; }
}
