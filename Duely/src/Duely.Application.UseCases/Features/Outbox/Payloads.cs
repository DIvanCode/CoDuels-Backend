namespace Duely.Application.UseCases.Payloads;
public interface IOutboxPayload { }
public sealed record TestSolutionPayload(string TaskId, int SubmissionId, string Code, string Language) : IOutboxPayload;
public sealed record SendMessagePayload(int UserId, string Message) : IOutboxPayload;