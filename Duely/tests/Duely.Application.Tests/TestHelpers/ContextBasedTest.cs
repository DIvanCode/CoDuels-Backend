using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Duely.Infrastructure.DataAccess.EntityFramework;

namespace Duely.Application.Tests.TestHelpers;

public abstract class ContextBasedTest : IAsyncLifetime
{
    protected ContextBasedTest()
    {
        var builder = new DbContextOptionsBuilder<Context>();
        builder.UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        Context = new Context(builder.Options);
    }

    protected Context Context { get; }

    public async Task InitializeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Context.DisposeAsync().AsTask();
}
