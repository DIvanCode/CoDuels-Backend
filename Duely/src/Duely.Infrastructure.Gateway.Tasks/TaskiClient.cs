using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

    public async Task<Result<IReadOnlyList<TaskiSolutionEvent>>> GetSolutionEventsAsync(
        string solutionId,
        int startId,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var uri = $"solutions/{solutionId}/messages?start_id={startId}&count={count}";
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "solution messages request failed {StatusCode}, SolutionId={SolutionId}, StartId={StartId}, Count={Count}",
                    response.StatusCode,
                    solutionId,
                    startId,
                    count);
                return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>($"failed to get messages for solution {solutionId}");
            }

            var payload = await response.Content.ReadFromJsonAsync<GetSolutionMessagesResponse>(cancellationToken);
            if (payload is null)
            {
                logger.LogWarning(
                    "solution messages returned empty body, SolutionId={SolutionId}, StartId={StartId}, Count={Count}",
                    solutionId,
                    startId,
                    count);
                return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>("solution messages empty response");
            }

            if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "solution messages returned non-OK status. SolutionId={SolutionId}, StartId={StartId}, Count={Count}, Status={Status}",
                    solutionId,
                    startId,
                    count,
                    payload.Status);
                return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>($"solution messages non-OK status: {payload.Status}");
            }

            return Result.Ok<IReadOnlyList<TaskiSolutionEvent>>(payload.Events);
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get solution messages, SolutionId={SolutionId}", solutionId);
            return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>(e.Message);
        }
    }

    private sealed class GetSolutionMessagesResponse
    {
        [JsonPropertyName("status"), Required]
        public required string Status { get; init; }

        [JsonPropertyName("messages")]
        public IReadOnlyList<TaskiSolutionEvent> Events { get; init; } = [];
    }
}
