using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Duely.Domain.Models.UserActions;

namespace Duely.Infrastructure.Api.Http.Requests.UserActions;

public sealed class SaveUserActionsRequest
{
    [JsonPropertyName("actions"), Required]
    public required IReadOnlyList<UserAction> Actions { get; init; }
}
