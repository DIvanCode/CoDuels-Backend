using System.Text;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duely.Domain.Services;

public static class ServiceCollectionExtensions
{
    public static void SetupDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DuelOptions>(configuration.GetSection(DuelOptions.SectionName));
        services.AddSingleton<IDuelManager, DuelManager>();
        
        services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));
        services.AddTransient<ITokenService, TokenService>();

        var jwtTokenOptions = configuration.GetSection(JwtTokenOptions.SectionName).Get<JwtTokenOptions>();
        ArgumentNullException.ThrowIfNull(jwtTokenOptions, nameof(jwtTokenOptions));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(
                JwtBearerDefaults.AuthenticationScheme,
                configureOptions =>
                {
                    configureOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtTokenOptions.SecretKey))
                    };
                });
    }
}