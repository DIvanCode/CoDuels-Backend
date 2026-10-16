using System.Data.Common;
using System.Text.Json;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Duels;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Duely.Application.Tests.Handlers;

public sealed class GetActiveDuelIdHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task Returns_null_when_user_has_no_duel()
    {
        Context.Users.Add(EntityFactory.MakeUser(1));
        await Context.SaveChangesAsync();

        var result = await QueryAsync(1);

        result.DuelId.Should().BeNull();
        JsonSerializer.Serialize(result).Should().Be("{\"duel_id\":null}");
    }

    [Fact]
    public async Task Returns_id_for_either_participant()
    {
        var user1 = EntityFactory.MakeUser(1, "user1");
        var user2 = EntityFactory.MakeUser(2, "user2");
        Context.Duels.Add(EntityFactory.MakeDuel(10, user1, user2));
        await Context.SaveChangesAsync();

        (await QueryAsync(1)).DuelId.Should().Be(10);
        (await QueryAsync(2)).DuelId.Should().Be(10);
        (await QueryAsync(3)).DuelId.Should().BeNull();
    }

    [Fact]
    public async Task Finished_history_is_not_active()
    {
        var user1 = EntityFactory.MakeUser(1, "user1");
        var user2 = EntityFactory.MakeUser(2, "user2");
        var finished = EntityFactory.MakeDuel(10, user1, user2);
        finished.Status = DuelStatus.Finished;
        Context.Duels.Add(finished);
        await Context.SaveChangesAsync();

        (await QueryAsync(1)).DuelId.Should().BeNull();
    }

    [Fact]
    public async Task Multiple_active_rows_choose_latest_start_then_highest_id()
    {
        var user1 = EntityFactory.MakeUser(1, "user1");
        var user2 = EntityFactory.MakeUser(2, "user2");
        var user3 = EntityFactory.MakeUser(3, "user3");
        var user4 = EntityFactory.MakeUser(4, "user4");
        var start = DateTime.UtcNow;
        Context.Duels.AddRange(
            EntityFactory.MakeDuel(10, user1, user2, start: start.AddMinutes(-1)),
            EntityFactory.MakeDuel(11, user1, user3, start: start),
            EntityFactory.MakeDuel(12, user4, user1, start: start));
        await Context.SaveChangesAsync();

        (await QueryAsync(1)).DuelId.Should().Be(12);
    }

    [Fact]
    public async Task Sqlite_query_selects_only_id_in_one_statement()
    {
        var interceptor = new QueryCaptureInterceptor();
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Context>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new Context(options);
        await context.Database.EnsureCreatedAsync();
        var user1 = EntityFactory.MakeUser(1, "user1");
        var user2 = EntityFactory.MakeUser(2, "user2");
        context.Duels.Add(EntityFactory.MakeDuel(10, user1, user2));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        interceptor.Queries.Clear();

        var result = await new GetActiveDuelIdHandler(context).Handle(
            new GetActiveDuelIdQuery { UserId = 1 }, CancellationToken.None);

        result.DuelId.Should().Be(10);
        interceptor.Queries.Should().ContainSingle();
        var sql = interceptor.Queries.Single();
        sql.Should().Contain("SELECT").And.Contain("Id");
        sql.Should().NotContain("Submissions").And.NotContain("Configuration");
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private Task<Duely.Application.UseCases.Dtos.ActiveDuelIdDto> QueryAsync(int userId) =>
        new GetActiveDuelIdHandler(Context).Handle(
            new GetActiveDuelIdQuery { UserId = userId }, CancellationToken.None);

    private sealed class QueryCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
