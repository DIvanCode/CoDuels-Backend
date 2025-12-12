using Duely.Domain.Models.Messages;
namespace Duely.Application.UseCases.Payloads;

public sealed record SendMessagePayload(int UserId, MessageType Type, int DuelId) : IOutboxPayload;