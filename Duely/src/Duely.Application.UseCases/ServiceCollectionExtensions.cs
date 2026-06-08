using System.Reflection;
using Duely.Application.UseCases.Dto.Tournaments.Configurations.Factories;
using Duely.Domain.Models.Tournaments.Entities.Configurations;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.UseCases;

public static class ServiceCollectionExtensions
{
    public static void SetupUseCases(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddSingleton<ITournamentConfigurationDtoFactoryResolver, TournamentConfigurationDtoFactoryResolver>();
        
        services.AddSingleton<ITournamentConfigurationDtoFactory, GroupStageTournamentConfigurationDtoFactory>();
        services.AddSingleton<ITournamentConfigurationDtoFactory<GroupStageTournamentConfiguration>,
            GroupStageTournamentConfigurationDtoFactory>();
        
        services.AddSingleton<ITournamentConfigurationDtoFactory,
            SingleEliminationBracketTournamentConfigurationDtoFactory>();
        services.AddSingleton<ITournamentConfigurationDtoFactory<SingleEliminationBracketTournamentConfiguration>,
            SingleEliminationBracketTournamentConfigurationDtoFactory>();
    }
}
