using FluentResults;

namespace Duely.Infrastructure.Gateway.Exesh.Abstracts;

public interface IExeshClient
{
    Task<Result<ExecuteResponse>> ExecuteAsync(ExeshStep[] steps, CancellationToken ct);
}

public sealed record ExecuteResponse(string ExecutionId);
