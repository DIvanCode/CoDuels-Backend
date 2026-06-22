// using Duely.Domain.Kernel.Entities;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Tournaments.Entities.Configurations;
//
// namespace Duely.Domain.Models.Tournaments.Entities;
//
// public abstract class TournamentConfiguration : ValueObject
// {
//     protected TournamentConfiguration(TournamentConfigurationType type, DuelConfiguration duelConfiguration)
//     {
//         Type = type;
//         DuelConfiguration = duelConfiguration;
//     }
//     
//     public TournamentConfigurationType Type { get; init; }
//     public DuelConfiguration DuelConfiguration { get; init; }
//
//     public static TournamentConfiguration Create(TournamentConfigurationType type, DuelConfiguration duelConfiguration)
//     {
//         switch (type)
//         {
//             case TournamentConfigurationType.SingleEliminationBracket:
//                 return new SingleEliminationBracketTournamentConfiguration(duelConfiguration);
//             case TournamentConfigurationType.GroupStage:
//                 return new GroupStageTournamentConfiguration(duelConfiguration);
//             default:
//                 throw new ArgumentOutOfRangeException(nameof(type), type, null);
//         }
//     }
//
//     internal abstract void Build(IReadOnlyCollection<TournamentParticipant> participants);
//     
//     protected override IEnumerable<object?> GetEqualityComponents()
//     {
//         yield return Type;
//         yield return DuelConfiguration;
//     }
// }
//
// public enum TournamentConfigurationType
// {
//     SingleEliminationBracket = 0,
//     GroupStage = 1
// }
