using System.Text.Json.Serialization;

public sealed class TaskiTask
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("level")]
    public required int Level { get; init; }

    [JsonPropertyName("statement")]
    public required string Statement { get; init; }

    [JsonPropertyName("tl")]
    public required int TimeLimit { get; init; }

    [JsonPropertyName("ml")]
    public required int MemoryLimit { get; init; }

    public List<TaskTest> Tests { get; init; } = new();
}