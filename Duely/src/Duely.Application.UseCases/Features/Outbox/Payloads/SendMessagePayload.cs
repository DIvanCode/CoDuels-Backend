namespace Duely.Application.UseCases.Payloads;

public sealed record SendMessagePayload(int UserId, string Message) : IOutboxPayload;