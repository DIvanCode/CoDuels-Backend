using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Exesh;

public sealed class ExeshClient(HttpClient http) : IExeshClient
{
    public async Task<Result<ExecuteResponse>> ExecuteAsync(
        ExecuteCodeRequest executeCodeRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = ExecutionFactory.Build(executeCodeRequest);
            using var resp = await http.PostAsJsonAsync("execute", request, cancellationToken);

            if (!resp.IsSuccessStatusCode)
                return Result.Fail("Failed to execute code via Exesh");

            var dto = await resp.Content.ReadFromJsonAsync<ExecuteDto>(cancellationToken: cancellationToken);
            if (dto is null)
                return Result.Fail("Invalid response from Exesh");

            if (!string.Equals(dto.Status, "OK", StringComparison.OrdinalIgnoreCase))
                return Result.Fail(dto.Error ?? "Exesh returned non-OK status");

            if (string.IsNullOrWhiteSpace(dto.ExecutionId))
                return Result.Fail("Exesh returned empty execution_id");

            return Result.Ok(new ExecuteResponse(dto.ExecutionId));
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ExeshExecutionEvent>>> GetExecutionEventsAsync(
        string executionId,
        int startId,
        int count,
        CancellationToken ct)
    {
        try
        {
            var uri = $"executions/{executionId}/messages?start_id={startId}&count={count}";
            using var resp = await http.GetAsync(uri, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return Result.Fail<IReadOnlyList<ExeshExecutionEvent>>(
                    $"failed to get messages for execution {executionId}");
            }

            var payload = await resp.Content.ReadFromJsonAsync<GetExecutionMessagesResponse>(ct);
            if (payload is null)
            {
                return Result.Fail<IReadOnlyList<ExeshExecutionEvent>>("execution messages empty response");
            }

            if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail<IReadOnlyList<ExeshExecutionEvent>>(
                    $"execution messages non-OK status: {payload.Status}");
            }

            return Result.Ok<IReadOnlyList<ExeshExecutionEvent>>(payload.Events);
        }
        catch (Exception ex)
        {
            return Result.Fail<IReadOnlyList<ExeshExecutionEvent>>(ex.Message);
        }
    }

    private sealed class ExecuteDto
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("execution_id")]
        public string ExecutionId { get; set; } = "";

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class GetExecutionMessagesResponse
    {
        [JsonPropertyName("status"), Required]
        public required string Status { get; init; }
        
        [JsonPropertyName("messages")]
        public IReadOnlyList<ExeshExecutionEvent> Events { get; init; } = [];
    }

}
