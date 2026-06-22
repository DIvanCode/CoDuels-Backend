// using System.Collections.ObjectModel;
// using Duely.Domain.Models.Tournaments.Entities;
//
// namespace Duely.Application.Handlers.Dto.Tournaments.Configurations.Factories;
//
// internal interface ITournamentConfigurationDtoFactoryResolver
// {
//     ITournamentConfigurationDtoFactory GetFactory(TournamentConfigurationType tournamentConfigurationType);
// }
//
// internal sealed class TournamentConfigurationDtoFactoryResolver : ITournamentConfigurationDtoFactoryResolver
// {
//     private readonly IReadOnlyDictionary<TournamentConfigurationType, ITournamentConfigurationDtoFactory> _factories;
//
//     public TournamentConfigurationDtoFactoryResolver(IEnumerable<ITournamentConfigurationDtoFactory> factories)
//     {
//         _factories = factories.ToDictionary(factory => factory.SupportedType, factory => factory);
//     }
//     
//     public ITournamentConfigurationDtoFactory GetFactory(TournamentConfigurationType tournamentConfigurationType)
//     {
//         return _factories[tournamentConfigurationType];
//     }
// }
