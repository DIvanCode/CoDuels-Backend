using System.Net.Http.Json;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class TaskiClient(HttpClient httpClient, ILogger<TaskiClient> logger) : ITaskiClient
{
    public async Task<Result> TestSolutionAsync(
        string taskId,
        string solutionId,
        string solution,
        string language,
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

            using var resp = await httpClient.PostAsJsonAsync("test", request, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Taski test failed. StatusCode = {StatusCode}, TaskId = {TaskId}, SolutionId = {SolutionId}",
                    (int)resp.StatusCode, taskId, solutionId
                );

                return Result.Fail($"Failed to test solution {solutionId} for task {taskId}");
            }

            return Result.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Taski test request crashed. TaskId = {TaskId}, SolutionId = {SolutionId}", taskId, solutionId);

            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resp = await httpClient.GetFromJsonAsync<RandomTaskResponse>("task/random", cancellationToken);
            if (resp is null)
            {
                logger.LogWarning("Taski random task returned empty response");

                return Result.Fail<string>("No random task returned from Taski");
            }

            return Result.Ok(resp.TaskId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Taski random task request crashed");

            return Result.Fail<string>(e.Message);
        }
    }

    public async Task<Result<TaskListResponse>> GetTasksListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resp = await httpClient.GetFromJsonAsync<TaskListResponse>(
                "task/list",
                cancellationToken);
            if (resp is null)
            {
                logger.LogWarning("Taski tasks list returned empty response");

                return Result.Fail<TaskListResponse>("No tasks returned from Taski");
            }

            return Result.Ok(resp);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Taski tasks list request crashed");

            return Result.Fail<TaskListResponse>(e.Message);
        }
    }
}