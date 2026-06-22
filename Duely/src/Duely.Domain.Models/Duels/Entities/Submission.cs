// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Users;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Duels;
//
// public sealed class Submission
// {
//     public int Id { get; init; }
//     public required User Owner { get; init; }
//     public required Duel Duel { get; init; }
//     public required char TaskKey { get; init; }
//     
//     public required string Text { get; set; }
//     public required Language Language { get; set; }
//     
//     public required SubmissionStatus Status { get; set; }
//     public required DateTime SubmitTime { get; init; }
//     public required bool IsUpsolving { get; init; }
//     
//     public string? Verdict { get; set; }
//     public string? Message { get; set; }
//     public int LastHandledStatusSeqId { get; set; }
// }
//
// public enum SubmissionStatus
// {
//     New = 0,
//     Queued = 1,
//     Running = 2,
//     Done = 3
// }
