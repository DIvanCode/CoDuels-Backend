using Duely.Domain.Models.Tournaments.Entities;

namespace Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;

internal interface ITournamentConfigurationDtoFactoryResolver
{
    ITournamentConfigurationDtoFactory GetFactory(TournamentConfigurationType tournamentConfigurationType);
}

internal sealed class TournamentConfigurationDtoFactoryResolver : ITournamentConfigurationDtoFactoryResolver
{
    private readonly IReadOnlyDictionary<TournamentConfigurationType, ITournamentConfigurationDtoFactory> _factories;

    public TournamentConfigurationDtoFactoryResolver(
        Dictionary<TournamentConfigurationType, ITournamentConfigurationDtoFactory> factories)
    {
        _factories = factories.AsReadOnly();
    }
    
    public ITournamentConfigurationDtoFactory GetFactory(TournamentConfigurationType tournamentConfigurationType)
    {
        return _factories[tournamentConfigurationType];
    }
}
