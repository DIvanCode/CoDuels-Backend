using FluentResults;

namespace Duely.Infrastructure.Problems.Abstracts;

public interface IProblemsGateway
{
    Task<Result<List<ProblemResponse>>> GetProblemsListAsync(string systemName, CancellationToken cancellationToken);
    
    // Task<Result> TestSolutionAsync(
    //     string taskId,
    //     string solutionId,
    //     string solution,
    //     Language language,
    //     CancellationToken cancellationToken);

    // Task<Result<IReadOnlyList<TaskiSolutionEvent>>> GetSolutionEventsAsync(
    //     string solutionId,
    //     int startId,
    //     int count,
    //     CancellationToken cancellationToken);
}
