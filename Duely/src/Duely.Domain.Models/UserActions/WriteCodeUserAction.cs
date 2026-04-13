using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duely.Domain.Models.UserActions;

public sealed class WriteCodeUserAction : UserAction
{
    [JsonIgnore, NotMapped]
    public override UserActionType Type => UserActionType.WriteCode;

    [JsonPropertyName("code_length"), Required]
    public required int CodeLength { get; init; }

    [JsonPropertyName("cursor_line"), Required]
    public required int CursorLine { get; init; }
}
