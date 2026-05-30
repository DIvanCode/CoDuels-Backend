using System.Reflection;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Tournaments;
using Duely.Domain.Services.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Domain.Services;

public static class ServiceCollectionExtensions
{
    public static void SetupDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UserOptions>(configuration.GetSection(UserOptions.SectionName));
        
        services.AddScoped<IGroupPermissionsService, GroupPermissionsService>();
        
        services.Configure<DuelOptions>(configuration.GetSection(DuelOptions.SectionName));
        services.AddSingleton<IDuelManager, DuelManager>();

        services.AddScoped<IRatingManager, RatingManager>();
        services.AddScoped<ITaskService, TaskService>();
        foreach (var strategyType in GetConcreteTypes<ITournamentMatchmakingStrategy>(typeof(ServiceCollectionExtensions).Assembly))
        {
            services.AddScoped(typeof(ITournamentMatchmakingStrategy), strategyType);
        }
        services.AddScoped<ITournamentMatchmakingStrategyResolver, TournamentMatchmakingStrategyResolver>();
    }

    private static IEnumerable<Type> GetConcreteTypes<TService>(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => typeof(TService).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false });
    }
}
