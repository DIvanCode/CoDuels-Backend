using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;

namespace Duely.Application.Tests.TestHelpers;

public sealed class TaskiClientSuccessFake : ITaskiClient
{
    private readonly string _taskId;
    private readonly bool _testSolutionSucceeds;

    public TaskiClientSuccessFake(string taskId = "TASK-1", bool testSolutionSucceeds = true)
    {
        _taskId = taskId;
        _testSolutionSucceeds = testSolutionSucceeds;
    }

    public Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken)
        => Task.FromResult(Result.Ok(_taskId));

    public Task<Result> TestSolutionAsync(
        string taskId, string solutionId, string solution, string language, CancellationToken cancellationToken)
        => Task.FromResult(_testSolutionSucceeds ? Result.Ok() : Result.Fail("forced-fail"));
}

public sealed class TaskiClientFailFake : ITaskiClient
{
    private readonly bool _testSolutionFails;
    private readonly string _error;

    public TaskiClientFailFake(string error = "boom", bool testSolutionFails = false)
    {
        _error = error;
        _testSolutionFails = testSolutionFails;
    }

    public Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken)
        => Task.FromResult(Result.Fail<string>(_error));

    public Task<Result> TestSolutionAsync(
        string taskId, string solutionId, string solution, string language, CancellationToken cancellationToken)
        => Task.FromResult(_testSolutionFails ? Result.Fail("forced-fail") : Result.Ok());
}
