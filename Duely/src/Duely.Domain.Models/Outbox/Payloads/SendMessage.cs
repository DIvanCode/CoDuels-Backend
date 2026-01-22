using System.Text.Json.Serialization;
using Duely.Domain.Models.Messages;

namespace Duely.Domain.Models.Outbox.Payloads;

public sealed class SendMessagePayload : OutboxPayload
{
    [JsonPropertyName("user_id")]
    public required int UserId { get; init; }

    [JsonPropertyName("message")]
    public required Message Message { get; init; }
}
