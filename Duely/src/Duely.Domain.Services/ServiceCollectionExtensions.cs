// using Duely.Domain.Services.Duels;
// using Duely.Domain.Services.Groups;
using Duely.Domain.Services.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Domain.Services;

public static class ServiceCollectionExtensions
{
    public static void SetupDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UserOptions>(configuration.GetSection(UserOptions.SectionName));
        
        // services.AddSingleton<IGroupPermissionsService, GroupPermissionsService>();
        
        // services.Configure<DuelOptions>(configuration.GetSection(DuelOptions.SectionName));
    }
}
