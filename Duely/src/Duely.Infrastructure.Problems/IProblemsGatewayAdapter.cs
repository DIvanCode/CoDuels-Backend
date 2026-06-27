using Duely.Infrastructure.Problems.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Problems;

public interface IProblemsGatewayAdapter
{
    string GatewayName { get; }

    Task<Result<List<ProblemResponse>>> GetProblemsListAsync(CancellationToken cancellationToken);
}
