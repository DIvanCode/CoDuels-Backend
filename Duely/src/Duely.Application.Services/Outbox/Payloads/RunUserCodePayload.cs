namespace Duely.Application.Services.Outbox.Payloads;

public sealed record RunUserCodePayload(int RunId, string Code, string Language, string Input) : IOutboxPayload;