using Duely.Application.UseCases;
using Duely.Infrastructure.Api.Http;
using Duely.Infrastructure.DataAccess.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

builder.Services.SetupApiHttp();
builder.Services.SetupUseCases();
builder.Services.SetupDataAccessEntityFramework(builder.Configuration);

var app = builder.Build();

app.UseApiHttp();
app.Run();