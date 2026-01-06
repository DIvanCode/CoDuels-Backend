using FluentResults;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public interface ITaskiClient
{
    Task<Result> TestSolutionAsync(
        string taskId,
        string solutionId,
        string solution,
        string language,
        CancellationToken cancellationToken);
    
    Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken);
}
