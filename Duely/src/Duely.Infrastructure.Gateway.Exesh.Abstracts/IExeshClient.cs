using FluentResults;

namespace Duely.Infrastructure.Gateway.Exesh.Abstracts;

public interface IExeshClient
{
    Task<Result<ExecuteResponse>> ExecuteAsync(ExecuteCodeRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<ExeshExecutionEvent>>> GetExecutionEventsAsync(
        string executionId,
        int startId,
        int count,
        CancellationToken ct);
}

public sealed record ExecuteCodeRequest(
    string Code,
    string Language,
    string Input,
    int DuelId,
    int UserId,
    string TaskKey,
    int TimeLimit,
    int MemoryLimit);

public sealed record ExecuteResponse(string ExecutionId);
