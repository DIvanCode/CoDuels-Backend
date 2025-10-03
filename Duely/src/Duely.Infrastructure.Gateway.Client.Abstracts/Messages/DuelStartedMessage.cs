namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public sealed class DuelStartedMessage : Message
{
    public override MessageType Type => MessageType.DuelStarted;
    public int DuelId { get; init; }
    public int User1Id { get; init; }
    public int User2Id { get; init; }
    public string TaskId { get; init; } = string.Empty;
}
