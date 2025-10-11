namespace Duely.Infrastructure.Gateway.Client.Abstracts.Messages;

public sealed class DuelFinishedMessage : Message
{
    public override MessageType Type => MessageType.DuelFinished;
    public int DuelId { get; init; }
    public string Winner { get; init; } = string.Empty;
}
