using Duely.Infrastructure.Gateway.Analyzer.Abstracts;
using FluentResults;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Duely.Infrastructure.Gateway.Analyzer;

public sealed class AnalyzerClient(HttpClient httpClient, ILogger<AnalyzerClient> logger) : IAnalyzerClient
{
    private const string PredictUri = "predict";

    public async Task<Result<PredictResponse>> PredictAsync(PredictRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(PredictUri, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("predict request failed with status {StatusCode}", response.StatusCode);
                return Result.Fail<PredictResponse>("failed to predict score");
            }

            var payload = await response.Content.ReadFromJsonAsync<PredictResponse>(cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("predict response body is empty");
                return Result.Fail<PredictResponse>("empty predict response");
            }

            return Result.Ok(payload);
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to predict score");
            return Result.Fail<PredictResponse>(e.Message);
        }
    }
}
