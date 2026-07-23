using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.TestHelpers;

internal sealed class TestContextFactory(DbContextOptions<Context> options) : IDbContextFactory<Context>
{
    public Context CreateDbContext()
    {
        return new Context(options);
    }

    public Task<Context> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
