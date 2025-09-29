namespace Duely.Infrastructure.Gateway.Client.Abstracts;

public sealed class SubmissionUpdateMessage : Message
{
    public override MessageType Type => MessageType.SubmissionUpdate;
    public int SubmissionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Verdict { get; init; }
}
