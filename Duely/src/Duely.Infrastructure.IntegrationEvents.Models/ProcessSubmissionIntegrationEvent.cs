using System.ComponentModel;
using Duely.Infrastructure.Gateway.Client.Abstracts;

namespace Duely.Infrastructure.IntegrationEvents.Models;

public sealed class ProcessSubmissionIntegrationEvent : IntegrationEvent
{
    public ProcessSubmissionIntegrationEvent(
        DateTime createdAt,
        DateTime attemptProcessAt,
        int submissionId)
        : base(IntegrationEventType.SendMessage, createdAt, attemptProcessAt)
    {
        SubmissionId = submissionId;
    }

    public int SubmissionId { get; init; }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private ProcessSubmissionIntegrationEvent()
        : base(IntegrationEventType.ProcessSubmission, default, default)
    {
    }
#pragma warning restore CS8618, CS9264
}
