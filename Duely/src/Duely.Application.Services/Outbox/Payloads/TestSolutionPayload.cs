namespace Duely.Application.Services.Outbox.Payloads;

public sealed record TestSolutionPayload(string TaskId, int SubmissionId, string Code, string Language) : IOutboxPayload;