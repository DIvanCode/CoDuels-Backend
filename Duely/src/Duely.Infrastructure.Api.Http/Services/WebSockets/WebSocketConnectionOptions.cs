namespace Duely.Infrastructure.Api.Http.Services.WebSockets;

public sealed class WebSocketConnectionOptions
{
    public const string SectionName = "WebSocketConnection";

    public int KeepAliveIntervalMs { get; init; } = 15000;
    public int CloseTimeoutMs { get; init; } = 3000;
}
