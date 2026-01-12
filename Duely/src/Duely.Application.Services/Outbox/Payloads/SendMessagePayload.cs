using Duely.Domain.Models.Messages;

namespace Duely.Application.Services.Outbox.Payloads;

public sealed record SendMessagePayload(
    int UserId,
    MessageType Type,
    int DuelId,
    string? OpponentNickname = null) : IOutboxPayload;
