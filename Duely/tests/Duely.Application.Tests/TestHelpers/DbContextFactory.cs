using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Duely.Infrastructure.DataAccess.EntityFramework;

namespace Duely.Application.Tests.TestHelpers;

public static class DbContextFactory
{
    public static (Context ctx, DbConnection conn) CreateSqliteContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var opts = new DbContextOptionsBuilder<Context>()
            .UseSqlite(conn)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new Context(opts);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }
}
