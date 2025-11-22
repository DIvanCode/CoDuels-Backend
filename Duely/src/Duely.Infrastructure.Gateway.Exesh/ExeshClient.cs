using System.Net.Http.Json;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentResults;

namespace Duely.Infrastructure.Gateway.Exesh;

public sealed class ExeshClient(HttpClient http) : IExeshClient
{
    public async Task<Result<ExecuteResponse>> ExecuteAsync(object[] steps, CancellationToken ct)
    {
        try
        {
            var request = new { steps };

            using var resp = await http.PostAsJsonAsync("execute", request, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return Result.Fail("Failed to execute code via Exesh");
            }

            var dto = await resp.Content.ReadFromJsonAsync<ExecuteDto>(cancellationToken: ct);
            if (dto is null)
            {
                return Result.Fail("Invalid response from Exesh");
            }

            return Result.Ok(new ExecuteResponse(dto.ExecutionId));
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private sealed class ExecuteDto
    {
        public string ExecutionId { get; set; } = "";
    }
}
