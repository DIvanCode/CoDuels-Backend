using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duely.Domain.Models.UserActions;

public sealed class RunCustomTestUserAction : UserAction
{
    [JsonIgnore, NotMapped]
    public override UserActionType Type => UserActionType.RunCustomTest;
}
