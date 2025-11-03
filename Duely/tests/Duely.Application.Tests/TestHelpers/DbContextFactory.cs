using System.Data.Common;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.TestHelpers;

public static class DbContextFactory
{
    public static (Context ctx, DbConnection conn) CreateSqliteContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new Context(options);
        ctx.Database.EnsureCreated();

        return (ctx, connection);
    }
}
