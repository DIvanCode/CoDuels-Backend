using Duely.Application.UseCases;
using Duely.Application.BackgroundJobs;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

builder.Services.SetupApiHttp();
builder.Services.SetupUseCases();
builder.Services.SetupDataAccessEntityFramework(builder.Configuration);
builder.Services.Configure<DuelSettings>(builder.Configuration.GetSection(DuelSettings.SectionName));

var app = builder.Build();

app.UseApiHttp();
app.Run();