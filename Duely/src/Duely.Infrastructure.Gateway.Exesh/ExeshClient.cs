using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Exesh;

public sealed class ExeshClient(HttpClient http) : IExeshClient
{
    public async Task<Result<ExecuteResponse>> ExecuteAsync(ExeshStep[] steps, CancellationToken cancellationToken)
    {
        try
        {
            var request = new ExecuteRequest(steps);
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

    private sealed record ExecuteRequest(
        [property: JsonPropertyName("steps")] IReadOnlyCollection<ExeshStep> Steps
    );

    private sealed class ExecuteDto
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("execution_id")]
        public string ExecutionId { get; set; } = "";

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
