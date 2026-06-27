using Duely.Infrastructure.Problems.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Problems;

public sealed class ProblemsGateway : IProblemsGateway
{
    private readonly Dictionary<string, IProblemsGatewayAdapter> _adapters;

    public ProblemsGateway(IEnumerable<IProblemsGatewayAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.GatewayName, a => a);
    }
    
    public async Task<Result<List<ProblemResponse>>> GetProblemsListAsync(
        string systemName,
        CancellationToken cancellationToken)
    {
        if (!_adapters.TryGetValue(systemName, out var adapter))
        {
            throw new ArgumentException($"Не найден адаптер для системы задач {systemName}");
        }
        
        return await adapter.GetProblemsListAsync(cancellationToken);
    }
}
