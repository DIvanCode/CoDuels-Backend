using Duely.Domain.Models.Duels.Entities;
using FluentResults;

namespace Duely.Infrastructure.Problems.Abstracts;

public interface IProblemsGateway
{
    Task<Result<List<ProblemResponse>>> GetProblemsListAsync(string systemName, CancellationToken cancellationToken);
    
    Task<Result<string>> TestSolutionAsync(
        string systemName,
        string problemExternalId,
        string source,
        Language language,
        CancellationToken cancellationToken);

    // Task<Result<IReadOnlyList<TaskiSolutionEvent>>> GetSolutionEventsAsync(
    //     string solutionId,
    //     int startId,
    //     int count,
    //     CancellationToken cancellationToken);
}
