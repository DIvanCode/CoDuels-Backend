namespace Duely.Application.UseCases.Payloads;

public sealed record RunUserCodePayload(int RunId , string Code, string Language, string Input) : IOutboxPayload;