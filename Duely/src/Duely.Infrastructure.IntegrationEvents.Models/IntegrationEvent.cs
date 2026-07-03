using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Duely.Infrastructure.IntegrationEvents.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = nameof(Type))]
[JsonDerivedType(typeof(SendMessageIntegrationEvent), nameof(IntegrationEventType.SendMessage))]
[JsonDerivedType(typeof(StartDuelIntegrationEvent), nameof(IntegrationEventType.StartDuel))]
[JsonDerivedType(typeof(ProcessSubmissionIntegrationEvent), nameof(IntegrationEventType.ProcessSubmission))]
public abstract class IntegrationEvent
{
    protected IntegrationEvent(IntegrationEventType type, DateTime createdAt, DateTime attemptProcessAt)
    {
        Type = type;
        CreatedAt = createdAt;
        Status = IntegrationEventStatus.New;
        ProcessAttempts = 0;
        NextProcessAttemptAt = attemptProcessAt;
    }

    public int Id { get; init; }
    public IntegrationEventType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public IntegrationEventStatus Status { get; private set; }
    public int ProcessAttempts { get; private set; }
    public DateTime NextProcessAttemptAt { get; private set; }

    public void Process(DateTime nextProcessAttemptAt)
    {
        Status = IntegrationEventStatus.Process;
        ProcessAttempts++;
        NextProcessAttemptAt = nextProcessAttemptAt;
    }

    public void Failed(DateTime nextProcessAttemptAt)
    {
        Status = IntegrationEventStatus.Failed;
        NextProcessAttemptAt = nextProcessAttemptAt;
    }
    
    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private IntegrationEvent()
    {
    }
#pragma warning restore CS8618, CS9264
}

public enum IntegrationEventType
{
    SendMessage = 0,
    StartDuel = 1,
    ProcessSubmission = 2
}

public enum IntegrationEventStatus
{
    New = 0,
    Process = 1,
    Failed = 2
}
