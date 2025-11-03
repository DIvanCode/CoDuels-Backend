using System.Threading;
using System.Threading.Tasks;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentResults;

namespace Duely.Application.Tests.TestHelpers;

public sealed class TaskiClientSuccessFake : ITaskiClient
{
    private readonly int _taskId;
    public TaskiClientSuccessFake(int taskId) => _taskId = taskId;
    public Task<Result<int>> GetRandomTaskIdAsync(CancellationToken ct) 
        => Task.FromResult(Result.Ok(_taskId));
}

public sealed class TaskiClientFailFake : ITaskiClient
{
    public Task<Result<int>> GetRandomTaskIdAsync(CancellationToken ct) 
        => Task.FromResult(Result.Fail<int>("boom"));
}
