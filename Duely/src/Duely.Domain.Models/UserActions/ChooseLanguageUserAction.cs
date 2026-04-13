using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using Duely.Domain.Models.Duels;

namespace Duely.Domain.Models.UserActions;

public sealed class ChooseLanguageUserAction : UserAction
{
    [JsonIgnore, NotMapped]
    public override UserActionType Type => UserActionType.ChooseLanguage;

    [JsonPropertyName("language"), JsonConverter(typeof(JsonStringEnumConverter)), Required]
    public required Language Language { get; init; }
}
