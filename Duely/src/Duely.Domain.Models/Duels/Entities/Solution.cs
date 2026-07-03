// using System.ComponentModel;
// using Duely.Domain.Kernel.Entities;
// using Duely.Domain.Models.Users.Entities;
//
// namespace Duely.Domain.Models.Duels.Entities;
//
// public sealed class Solution
// {
//     public Solution(User user, DuelProblem problem, string source, Language language)
//     {
//         User = user;
//         Problem = problem;
//         Source = source;
//         Language = language;
//     }
//     
//     public int Id { get; init; }
//     public User User { get; init; }
//     public DuelProblem Problem { get; init; }
//     public string Source { get; private set; }
//     public Language Language { get; private set; }
//     
//     // ReSharper disable once UnusedMember.Local
// #pragma warning disable CS8618, CS9264
//     /// <summary>
//     /// EF constructor. Do not use explicitly!
//     /// </summary>
//     [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
//     [EditorBrowsable(EditorBrowsableState.Never)]
//     private Solution()
//     {
//     }
// #pragma warning restore CS8618, CS9264
// }
