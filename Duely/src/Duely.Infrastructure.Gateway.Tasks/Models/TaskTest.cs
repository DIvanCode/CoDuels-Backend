using System.Text.Json.Serialization;

public sealed class TaskTest
{

    [JsonPropertyName("order")]
    public required int Order { get; init; }

    [JsonPropertyName("input")]
    public required string Input { get; init; }

    [JsonPropertyName("output")]
    public required string Output { get; init; }

}