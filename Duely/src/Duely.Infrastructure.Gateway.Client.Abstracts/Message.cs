namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public enum MessageType
{
    DuelStarted,
    SubmissionReceived,
    SubmissionUpdate,
    SubmissionVerdict,
    DuelFinished
}
public abstract class Message
{
    public abstract MessageType Type { get; }
}