using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Messages;

public enum MessageType
{
    DuelStarted = 0,
    DuelFinished = 1,
    DuelChanged = 2,
    OpponentSolutionUpdated = 4,
    DuelSearchCanceled = 5,
    DuelInvitation = 6,
    DuelInvitationCanceled = 7,
    DuelInvitationDenied = 8,
    SubmissionStatusUpdated = 9,
    CodeRunStatusUpdated = 10
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DuelStartedMessage), nameof(MessageType.DuelStarted))]
[JsonDerivedType(typeof(DuelFinishedMessage), nameof(MessageType.DuelFinished))]
[JsonDerivedType(typeof(DuelChangedMessage), nameof(MessageType.DuelChanged))]
[JsonDerivedType(typeof(OpponentSolutionUpdatedMessage), nameof(MessageType.OpponentSolutionUpdated))]
[JsonDerivedType(typeof(DuelSearchCanceledMessage), nameof(MessageType.DuelSearchCanceled))]
[JsonDerivedType(typeof(DuelInvitationMessage), nameof(MessageType.DuelInvitation))]
[JsonDerivedType(typeof(DuelInvitationCanceledMessage), nameof(MessageType.DuelInvitationCanceled))]
[JsonDerivedType(typeof(DuelInvitationDeniedMessage), nameof(MessageType.DuelInvitationDenied))]
[JsonDerivedType(typeof(SubmissionStatusUpdatedMessage), nameof(MessageType.SubmissionStatusUpdated))]
[JsonDerivedType(typeof(CodeRunStatusUpdatedMessage), nameof(MessageType.CodeRunStatusUpdated))]
public abstract class Message;
