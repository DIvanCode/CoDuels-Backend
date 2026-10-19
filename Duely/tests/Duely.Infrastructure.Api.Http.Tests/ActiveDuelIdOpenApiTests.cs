using Duely.Infrastructure.Api.Http.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class ActiveDuelIdOpenApiTests
{
    [Fact]
    public void Generated_swagger_describes_nullable_duel_id_response()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(DuelsController).Assembly);
        builder.Services.AddSwaggerGen();
        using var app = builder.Build();

        var swagger = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var operation = swagger.Paths["/duels/active/id"].Operations[OperationType.Get];
        var response = operation.Responses["200"];
        var schema = response.Content["application/json"].Schema;

        schema.Reference.Id.Should().Be("ActiveDuelIdDto");
        var property = swagger.Components.Schemas[schema.Reference.Id].Properties["duel_id"];
        property.Type.Should().Be("integer");
        property.Nullable.Should().BeTrue();
    }

    [Fact]
    public void Generated_swagger_describes_active_duel_ids_response()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(DuelsController).Assembly);
        builder.Services.AddSwaggerGen();
        using var app = builder.Build();

        var swagger = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var operation = swagger.Paths["/duels/active/ids"].Operations[OperationType.Get];
        var schema = operation.Responses["200"].Content["application/json"].Schema;

        schema.Reference.Id.Should().Be("ActiveDuelIdsDto");
        var property = swagger.Components.Schemas[schema.Reference.Id].Properties["duel_ids"];
        property.Type.Should().Be("array");
        property.Items.Type.Should().Be("integer");
    }
}
