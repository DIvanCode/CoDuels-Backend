using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Tasks.Abstracts;

public interface ITaskiClient
{
    Task<Result> TestSolutionAsync(
        string taskId,
        string solutionId,
        string solution,
        Language language,
        CancellationToken cancellationToken);
    
    Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TaskiSolutionEvent>>> GetSolutionEventsAsync(
        string solutionId,
        int startId,
        int count,
        CancellationToken cancellationToken);
}
