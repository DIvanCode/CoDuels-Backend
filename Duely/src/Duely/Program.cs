using Duely.Application.UseCases;
using Duely.Application.BackgroundJobs;
using Duely.Application.Services;
using Duely.Domain.Services;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks;
using Duely.Infrastructure.Gateway.Exesh;
using Duely.Infrastructure.Gateway.Analyzer;
using Duely.Infrastructure.MessageBus.Kafka;
using Duely.Infrastructure.BackgroundJobs;
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
builder.Services.SetupAnalyzerGateway(builder.Configuration);

builder.Services.Configure<TaskiStatusPollingOptions>(
    builder.Configuration.GetSection(TaskiStatusPollingOptions.SectionName));
builder.Services.Configure<ExeshStatusPollingOptions>(
    builder.Configuration.GetSection(ExeshStatusPollingOptions.SectionName));

var taskiStatusPollingOptions = builder.Configuration
    .GetSection(TaskiStatusPollingOptions.SectionName)
    .Get<TaskiStatusPollingOptions>();
var exeshStatusPollingOptions = builder.Configuration
    .GetSection(ExeshStatusPollingOptions.SectionName)
    .Get<ExeshStatusPollingOptions>();

if (string.Equals(taskiStatusPollingOptions?.Mode, "kafka", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.SetupTaskiConsumer(builder.Configuration);
}
else
{
    builder.Services.SetupTaskiStatusRestPoller();
}

if (string.Equals(exeshStatusPollingOptions?.Mode, "kafka", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.SetupExeshConsumer(builder.Configuration);
}
else
{
    builder.Services.SetupExeshStatusRestPoller();
}

builder.Services.SetupTelemetry(builder.Configuration);

var app = builder.Build();

app.UseApiHttp();
app.UseProblemDetails();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Logger.LogInformation("Duely started");

app.Run();
