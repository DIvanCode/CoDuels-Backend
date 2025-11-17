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

    Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken);
    
    Task<Result<IReadOnlyCollection<TaskResponse>>> GetTasksListAsync(CancellationToken cancellationToken);
}
