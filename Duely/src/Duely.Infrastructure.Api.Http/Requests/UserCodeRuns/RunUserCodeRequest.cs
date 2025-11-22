using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.UserCodeRuns;

public sealed class RunUserCodeRequest
{
    [JsonPropertyName("solution")]
    public required string Solution { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }
}
