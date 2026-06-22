// using Duely.Application.UseCases.Dto.Duels;
// using Duely.Domain.Models.Tournaments.Entities;
// using Duely.Domain.Models.Tournaments.Entities.Configurations;
//
// namespace Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
//
// public sealed class SingleEliminationBracketTournamentConfigurationDtoFactory
//     : ITournamentConfigurationDtoFactory,
//         ITournamentConfigurationDtoFactory<SingleEliminationBracketTournamentConfiguration>
// {
//     public TournamentConfigurationType SupportedType => TournamentConfigurationType.GroupStage;
//     
//     public TournamentConfigurationDto Create(TournamentConfiguration configuration)
//     {
//         return configuration.Type == SupportedType
//             ? Create((SingleEliminationBracketTournamentConfiguration) configuration)
//             : throw new ArgumentException("Некорректный тип конфигурации турнира.");
//     }
//
//     public TournamentConfigurationDto Create(SingleEliminationBracketTournamentConfiguration configuration)
//     {
//         return new SingleEliminationBracketTournamentConfigurationDto
//         {
//             Type = configuration.Type,
//             DuelConfiguration = new DuelConfigurationDto
//             {
//                 Id = configuration.DuelConfiguration.Id,
//                 ShouldShowOpponentSolution = configuration.DuelConfiguration.ShouldShowOpponentSolution,
//                 DurationMinutes = configuration.DuelConfiguration.DurationMinutes,
//                 ProblemsCount = configuration.DuelConfiguration.ProblemsCount,
//                 ProblemsOrder = configuration.DuelConfiguration.ProblemsOrder
//             }
//         };
//     }
// }