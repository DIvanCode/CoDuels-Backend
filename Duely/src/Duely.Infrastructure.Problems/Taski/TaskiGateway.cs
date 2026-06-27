using System.Net.Http.Json;
using Duely.Infrastructure.Problems.Abstracts;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Duely.Infrastructure.Problems.Taski;

internal sealed class TaskiProblemsGatewayAdapter(HttpClient httpClient, ILogger<TaskiProblemsGatewayAdapter> logger)
    : IProblemsGatewayAdapter
{
    private const string TaskListUri = "task/list";
    // private const string TestSolutionUri = "test";
    
    public string GatewayName => "Taski";
    
    public async Task<Result<List<ProblemResponse>>> GetProblemsListAsync(CancellationToken cancellationToken)
    {
        const string errorMessage = "Ошибка при получении списка задач из Taski.";
        
        try
        {
            var response = await httpClient.GetFromJsonAsync<TaskiTaskListResponse>(TaskListUri, cancellationToken);
            if (response is null)
            {
                logger.LogError("Failed to get tasks list from Taski: {Reason}", "empty response");
                return Result.Fail<List<ProblemResponse>>(errorMessage);
            }

            if (response.Status != TaskiResponseStatusCode.Ok)
            {
                logger.LogError("Failed to get tasks list from Taski: {Reason}", response.Error);
                return Result.Fail<List<ProblemResponse>>(errorMessage);
            }

            if (response.Tasks is null)
            {
                logger.LogError("Failed to get tasks list from Taski: {Reason}", "empty tasks list");
                return Result.Fail<List<ProblemResponse>>(errorMessage);
            }

            var problems = new List<ProblemResponse>();
            foreach (var taskResponse in response.Tasks)
            {
                if (taskResponse.Type != TaskiTaskType.WriteCode)
                {
                    continue;
                } 
                
                problems.Add(new ProblemResponse
                {
                    Id = taskResponse.Id,
                    Title = taskResponse.Title,
                });
            }

            return Result.Ok(problems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get tasks list from Taski: {Reason}", "exception");
            return Result.Fail<List<ProblemResponse>>(errorMessage);
        }
    }
    
    // public async Task<Result> TestSolutionAsync(
    //     string taskId,
    //     string solutionId,
    //     string solution,
    //     Language language,
    //     CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         var request = new TestRequest
    //         {
    //             TaskId = taskId,
    //             SolutionId = solutionId,
    //             Solution = solution,
    //             Language = language
    //         };
    //
    //         using var response = await httpClient.PostAsJsonAsync(TestSolutionUri, request, cancellationToken);
    //         if (!response.IsSuccessStatusCode)
    //         {
    //             logger.LogWarning("test solution request failed {StatusCode}, TaskId = {TaskId}, SolutionId = {SolutionId}",
    //                 response.StatusCode, taskId, solutionId
    //             );
    //             return Result.Fail($"failed to test solution {solutionId} for task {taskId}");
    //         }
    //
    //         return Result.Ok();
    //     }
    //     catch (Exception e)
    //     {
    //         logger.LogError(e, "failed to test solution");
    //         return Result.Fail(e.Message);
    //     }
    // }

    // public async Task<Result<IReadOnlyList<TaskiSolutionEvent>>> GetSolutionEventsAsync(
    //     string solutionId,
    //     int startId,
    //     int count,
    //     CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         var uri = $"solutions/{solutionId}/messages?start_id={startId}&count={count}";
    //         using var response = await httpClient.GetAsync(uri, cancellationToken);
    //         if (!response.IsSuccessStatusCode)
    //         {
    //             logger.LogWarning(
    //                 "solution messages request failed {StatusCode}, SolutionId={SolutionId}, StartId={StartId}, Count={Count}",
    //                 response.StatusCode,
    //                 solutionId,
    //                 startId,
    //                 count);
    //             return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>($"failed to get messages for solution {solutionId}");
    //         }
    //
    //         var payload = await response.Content.ReadFromJsonAsync<GetSolutionMessagesResponse>(cancellationToken);
    //         if (payload is null)
    //         {
    //             logger.LogWarning(
    //                 "solution messages returned empty body, SolutionId={SolutionId}, StartId={StartId}, Count={Count}",
    //                 solutionId,
    //                 startId,
    //                 count);
    //             return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>("solution messages empty response");
    //         }
    //
    //         if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
    //         {
    //             logger.LogWarning(
    //                 "solution messages returned non-OK status. SolutionId={SolutionId}, StartId={StartId}, Count={Count}, Status={Status}",
    //                 solutionId,
    //                 startId,
    //                 count,
    //                 payload.Status);
    //             return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>($"solution messages non-OK status: {payload.Status}");
    //         }
    //
    //         return Result.Ok<IReadOnlyList<TaskiSolutionEvent>>(payload.Events);
    //     }
    //     catch (Exception e)
    //     {
    //         logger.LogError(e, "failed to get solution messages, SolutionId={SolutionId}", solutionId);
    //         return Result.Fail<IReadOnlyList<TaskiSolutionEvent>>(e.Message);
    //     }
    // }

    // private sealed class GetSolutionMessagesResponse
    // {
    //     [JsonPropertyName("status"), Required]
    //     public required string Status { get; init; }
    //
    //     [JsonPropertyName("messages")]
    //     public IReadOnlyList<TaskiSolutionEvent> Events { get; init; } = [];
    // }
}
