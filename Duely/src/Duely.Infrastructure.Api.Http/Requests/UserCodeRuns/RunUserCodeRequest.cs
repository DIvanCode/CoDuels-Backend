using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Requests.UserCodeRuns;

public sealed class RunUserCodeRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }
}
