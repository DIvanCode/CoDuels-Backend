namespace Duely.Infrastructure.Gateway.Client.Abstracts.Messages;

public sealed class SubmissionVerdictMessage : Message
{
    public override MessageType Type => MessageType.SubmissionVerdict;

    public int SubmissionId { get; init; }
    public string Verdict { get; init; } = string.Empty;
    public string? Error { get; init; }
}
