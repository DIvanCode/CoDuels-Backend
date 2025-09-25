using Microsoft.AspNetCore.Builder;

namespace Duely.Infrastructure.Api.Http;

public static class WebApplicationExtensions
{
    public static void UseApiHttp(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapControllers();
    }
}