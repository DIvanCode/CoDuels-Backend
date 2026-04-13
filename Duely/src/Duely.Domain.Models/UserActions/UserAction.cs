using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Duely.Domain.Models.UserActions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChooseLanguageUserAction), nameof(UserActionType.ChooseLanguage))]
[JsonDerivedType(typeof(WriteCodeUserAction), nameof(UserActionType.WriteCode))]
[JsonDerivedType(typeof(DeleteCodeUserAction), nameof(UserActionType.DeleteCode))]
[JsonDerivedType(typeof(PasteCodeUserAction), nameof(UserActionType.PasteCode))]
[JsonDerivedType(typeof(CutCodeUserAction), nameof(UserActionType.CutCode))]
[JsonDerivedType(typeof(MoveCursorUserAction), nameof(UserActionType.MoveCursor))]
[JsonDerivedType(typeof(RunSampleTestUserAction), nameof(UserActionType.RunSampleTest))]
[JsonDerivedType(typeof(RunCustomTestUserAction), nameof(UserActionType.RunCustomTest))]
[JsonDerivedType(typeof(SubmitSolutionUserAction), nameof(UserActionType.SubmitSolution))]
public abstract class UserAction
{
    public int Id { get; init; }

    [JsonIgnore, NotMapped]
    public abstract UserActionType Type { get; }

    [JsonPropertyName("event_id"), Required]
    public required Guid EventId { get; init; }

    [JsonPropertyName("sequence_id"), Required]
    public required int SequenceId { get; init; }

    [JsonPropertyName("timestamp"), Required]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("duel_id"), Required]
    public required int DuelId { get; init; }

    [JsonPropertyName("task_key"), Required]
    public required char TaskKey { get; init; }

    [JsonPropertyName("user_id"), Required]
    public required int UserId { get; init; }
}

public enum UserActionType
{
    ChooseLanguage = 1,
    WriteCode = 2,
    DeleteCode = 3,
    PasteCode = 4,
    CutCode = 5,
    MoveCursor = 6,
    RunSampleTest = 7,
    RunCustomTest = 8,
    SubmitSolution = 9
}
