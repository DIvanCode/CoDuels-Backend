using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public static class ServiceCollectionExtensions
{
    public static void SetupDataAccessEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        var dbConnectionOptions = configuration.GetSection(DbConnectionOptions.SectionName).Get<DbConnectionOptions>();
        ArgumentNullException.ThrowIfNull(dbConnectionOptions, nameof(dbConnectionOptions));

        services.AddDbContext<Context>(options =>
        {
            options.UseNpgsql(
                dbConnectionOptions.ConnectionString,
                providerOptions =>
                {
                    providerOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
                });
        });
    }
}