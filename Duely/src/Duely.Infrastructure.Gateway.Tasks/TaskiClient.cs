using System.Net.Http.Json;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class TaskiClient(HttpClient httpClient, ILogger<TaskiClient> logger) : ITaskiClient
{
    private const string TestSolutionUri = "test";
    private const string TaskListUri = "task/list";
    
    public async Task<Result> TestSolutionAsync(
        string taskId,
        string solutionId,
        string solution,
        Language language,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new TestRequest
            {
                TaskId = taskId,
                SolutionId = solutionId,
                Solution = solution,
                Language = language
            };

            using var response = await httpClient.PostAsJsonAsync(TestSolutionUri, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("test solution request failed {StatusCode}, TaskId = {TaskId}, SolutionId = {SolutionId}",
                    response.StatusCode, taskId, solutionId
                );
                return Result.Fail($"failed to test solution {solutionId} for task {taskId}");
            }

            return Result.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to test solution");
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<TaskListResponse>(TaskListUri, cancellationToken);
            if (response is null)
            {
                logger.LogWarning("tasks list returned empty response");
                return Result.Fail<TaskListResponse>("tasks list empty response");
            }

            return Result.Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get tasks list");
            return Result.Fail<TaskListResponse>(e.Message);
        }
    }
}
