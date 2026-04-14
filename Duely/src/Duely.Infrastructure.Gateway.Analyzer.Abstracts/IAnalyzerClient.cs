using Duely.Domain.Models.UserActions;
using FluentResults;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Gateway.Analyzer.Abstracts;

public interface IAnalyzerClient
{
    Task<Result<PredictResponse>> PredictAsync(PredictRequest request, CancellationToken cancellationToken);
}

public sealed class PredictRequest
{
    [JsonPropertyName("actions")]
    public required IReadOnlyList<UserAction> Actions { get; init; }
}

public sealed class PredictResponse
{
    [JsonPropertyName("score")]
    public required float Score { get; init; }
}
