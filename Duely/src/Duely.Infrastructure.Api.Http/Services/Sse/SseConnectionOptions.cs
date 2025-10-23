namespace Duely.Infrastructure.Api.Http.Services.Sse;

public sealed class SseConnectionOptions
{
    public const string SectionName = "SseConnection";

    public int SsePingIntervalMs { get; init; } = 1000;
}
