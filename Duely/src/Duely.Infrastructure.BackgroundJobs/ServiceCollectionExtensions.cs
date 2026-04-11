using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.BackgroundJobs;

public static class ServiceCollectionExtensions
{
    public static void SetupTaskiStatusRestPoller(this IServiceCollection services)
    {
        services.AddHostedService<TaskiSubmissionStatusRestPoller>();
    }

    public static void SetupExeshStatusRestPoller(this IServiceCollection services)
    {
        services.AddHostedService<ExeshSubmissionStatusRestPoller>();
    }
}
