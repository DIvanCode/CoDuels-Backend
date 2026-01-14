namespace Duely.Domain.Models.Messages;

public enum MessageType
{
    DuelStarted = 0,
    DuelFinished = 1,
    DuelChanged = 2,
    DuelCanceled = 3
}

public abstract class Message
{
    public abstract MessageType Type { get; }
}
