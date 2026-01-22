using Duely.Domain.Models;
using FluentResults;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;

namespace Duely.Application.Tests.TestHelpers;

public sealed class TaskiClientSuccessFake : ITaskiClient
{
    private readonly string[] _tasks;
    private readonly bool _testSolutionSucceeds;

    public TaskiClientSuccessFake(string[]? tasks = null, bool testSolutionSucceeds = true)
    {
        _tasks = tasks ?? ["TASK-1"];
        _testSolutionSucceeds = testSolutionSucceeds;
    }

    public Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken)
        => Task.FromResult(Result.Ok(new TaskListResponse
        {
            Tasks = _tasks
                .Select(task => new TaskResponse
                {
                    Id = task,
                    Level = 1,
                    Topics = []
                })
                .ToList()
        }));

    public Task<Result> TestSolutionAsync(
        string taskId, string solutionId, string solution, Language language, CancellationToken cancellationToken)
        => Task.FromResult(_testSolutionSucceeds ? Result.Ok() : Result.Fail("forced-fail"));
}
