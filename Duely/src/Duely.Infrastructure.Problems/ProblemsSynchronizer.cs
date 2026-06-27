using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Infrastructure.Problems;

internal sealed class ProblemsSynchronizer(
    IEnumerable<IProblemsGatewayAdapter> gatewayAdapters,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ProblemsSynchronizerOptions> options,
    ILogger<ProblemsSynchronizer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Problems list synchronizer started with interval {Interval} ms",
            options.Value.IntervalMs);
        await Task.Yield();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WorkAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Problems list synchronizer unexpected error");
            }
            
            await Task.Delay(options.Value.IntervalMs, cancellationToken);
        }
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        foreach (var gatewayAdapter in gatewayAdapters)
        {
            var problemsResult = await gatewayAdapter.GetProblemsListAsync(cancellationToken);
            if (problemsResult.IsFailed)
            {
                logger.LogError("Failed to synchronize problems from {GatewayName}", gatewayAdapter.GatewayName);
                continue;
            }
            
            var gatewayProblems = problemsResult.Value
                .ToDictionary(p => p.Id, p => p);
            var databaseProblems = await context.Problems
                .Where(p => p.ExternalSystemName == gatewayAdapter.GatewayName)
                .ToDictionaryAsync(p => p.ExternalId, p => p, cancellationToken);
            
            var problemsToCreate = gatewayProblems.Keys.Except(databaseProblems.Keys);
            var problemsToUpdate = gatewayProblems.Keys.Intersect(databaseProblems.Keys);
            
            foreach (var problemId in problemsToCreate)
            {
                var gatewayProblem = gatewayProblems[problemId];
                var databaseProblem = new Problem(gatewayAdapter.GatewayName, gatewayProblem.Id, gatewayProblem.Title);
                
                context.Problems.Add(databaseProblem);
            }

            foreach (var problemId in problemsToUpdate)
            {
                var gatewayProblem = gatewayProblems[problemId];
                var databaseProblem = databaseProblems[problemId];
                
                databaseProblem.Update(gatewayProblem.Title);
                context.Problems.Update(databaseProblem);
            }
            
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}