using System.Net.Http.Json;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class TaskiClient(HttpClient httpClient) : ITaskiClient
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
                return Result.Fail($"Failed to test solution {solutionId} for task {taskId}");
            }

            return Result.Ok();
        }
        catch (Exception e)
        {
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
                return Result.Fail<string>("No random task returned from Taski");
            }

            return Result.Ok(resp.TaskId);
        }
        catch (Exception e)
        {
            return Result.Fail<string>(e.Message);
        }
    }

    public async Task<Result<IReadOnlyCollection<TaskResponse>>> GetTasksListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resp = await httpClient.GetFromJsonAsync<IReadOnlyCollection<TaskResponse>>(
                "task/list",
                cancellationToken);
            if (resp is null)
            {
                return Result.Fail<IReadOnlyCollection<TaskResponse>>("No tasks returned from Taski");
            }

            return Result.Ok(resp);
        }
        catch (Exception e)
        {
            return Result.Fail<IReadOnlyCollection<TaskResponse>>(e.Message);
        }
    }
}