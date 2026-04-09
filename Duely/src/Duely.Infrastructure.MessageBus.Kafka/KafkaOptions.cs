namespace Duely.Infrastructure.MessageBus.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public required string BootstrapServers { get; init; }
    public required string TaskiTopic { get; init; }
    public required string ExeshTopic { get; init; }
    public required string GroupId { get; init; }
    public required bool SaslAuth { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}
