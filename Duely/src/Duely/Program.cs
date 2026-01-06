using Duely.Application.UseCases;
using Duely.Application.BackgroundJobs;
using Duely.Application.Services;
using Duely.Domain.Services;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Exesh;
using Duely.Infrastructure.MessageBus.Kafka;
using Hellang.Middleware.ProblemDetails;
using Duely.Infrastructure.Telemetry;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Application
builder.Services.SetupApplicationServices(builder.Configuration);
builder.Services.SetupUseCases(builder.Configuration);
builder.Services.SetupBackgroundJobs(builder.Configuration);

// Domain
builder.Services.SetupDomainServices(builder.Configuration);

// Infrastructure
builder.Services.SetupApiHttp(builder.Configuration, builder.Environment);
builder.Services.SetupDataAccessEntityFramework(builder.Configuration);
builder.Services.SetupTasksGateway(builder.Configuration);
builder.Services.SetupExeshGateway(builder.Configuration);
builder.Services.SetupMessageBusKafka(builder.Configuration);
builder.Services.SetupTelemetry(builder.Configuration);

var app = builder.Build();

app.UseApiHttp();
app.UseProblemDetails();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Logger.LogInformation("Duely started");

app.Run();
