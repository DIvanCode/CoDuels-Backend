using System.Text.Json;
using Duely.Infrastructure.Api.Http.Services;
using Duely.Infrastructure.Api.Http.Services.Sse;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using FluentValidation;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using ProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

namespace Duely.Infrastructure.Api.Http;

public static class ServiceCollectionExtensions
{
    public static void SetupApiHttp(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IUserContext, UserContext>();

        services.Configure<SseConnectionOptions>(configuration.GetSection(SseConnectionOptions.SectionName));
        services.AddSingleton<ISseConnectionManager, SseConnectionManager>();
        services.AddSingleton<IMessageSender, SseMessageSender>();

        services.AddAuthorization();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddMvc();
        services.AddControllers();
        services.AddEndpointsApiExplorer();


        services.AddProblemDetails(options => ConfigureGlobalExceptionHandling(options, environment));

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter token (without 'Bearer')",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    private static void ConfigureGlobalExceptionHandling(ProblemDetailsOptions options, IWebHostEnvironment environment)
    {
        options.IncludeExceptionDetails = (_, _) => environment.IsDevelopment();
        options.ShouldLogUnhandledException =
            (httpContext, ex, _)
                => !(ex is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested);
        options.ValidationProblemStatusCode = StatusCodes.Status400BadRequest;
        options.Map<ValidationException>((context, exception) =>
        {
            var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var errors = exception.Errors
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
            return factory.CreateValidationProblemDetails(context, errors);
        });

        options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
        options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);
        options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
    }
}
