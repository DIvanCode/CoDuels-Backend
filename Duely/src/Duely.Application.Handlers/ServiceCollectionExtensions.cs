using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.Handlers;

public static class ServiceCollectionExtensions
{
    public static void SetupUseCases(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // services.AddSingleton<ITournamentConfigurationDtoFactoryResolver, TournamentConfigurationDtoFactoryResolver>();
        //
        // services.AddSingleton<ITournamentConfigurationDtoFactory, GroupStageTournamentConfigurationDtoFactory>();
        // services.AddSingleton<ITournamentConfigurationDtoFactory<GroupStageTournamentConfiguration>,
        //     GroupStageTournamentConfigurationDtoFactory>();
        //
        // services.AddSingleton<ITournamentConfigurationDtoFactory,
        //     SingleEliminationBracketTournamentConfigurationDtoFactory>();
        // services.AddSingleton<ITournamentConfigurationDtoFactory<SingleEliminationBracketTournamentConfiguration>,
        //     SingleEliminationBracketTournamentConfigurationDtoFactory>();
    }
}
