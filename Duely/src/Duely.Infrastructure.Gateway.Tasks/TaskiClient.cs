using System.Net.Http.Json;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.Gateway.Tasks.Abstracts.Models;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Tasks;

public sealed class TaskiClient : ITaskiClient
{
    private readonly HttpClient _http;

    public TaskiClient(HttpClient http) => _http = http;

    public async Task<Result> SendSubmission(
        string taskId,
        int submissionId,
        string solution,
        string language)
    {
        var request = new SendSubmissionRequest
        {
            TaskId =taskId,
            SubmissionId =submissionId.ToString(),
            Solution =solution,
            Language = language
        };

        using var resp = await _http.PostAsJsonAsync("test", request);
        if (!resp.IsSuccessStatusCode)
        {
            return Result.Fail($"Failed to send submission {submissionId} for task {taskId}");
        }
        return Result.Ok();
    }

    public async Task<Result<string>> GetRandomTaskIdAsync(CancellationToken cancellationToken)
    {
        return Result.Ok(Guid.NewGuid().ToString());
    }

    public async Task<Result<TaskiTask>> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<TaskiTask>($"task/{taskId}", cancellationToken);

            if (response is null)
            {
                return Result.Fail<TaskiTask>($"Task {taskId} not found");
            }

            return Result.Ok(response);
        }
        catch (Exception e)
        {
            return Result.Fail<TaskiTask>(e.Message);
        }
    }
}
