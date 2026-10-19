using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Handlers;

public sealed class RecordUserPresenceHandlerTests
{
    [Fact]
    public async Task Records_utc_presence_and_never_moves_it_backwards()
    {
        var (context, connection) = DbContextFactory.CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;
        context.Users.Add(new User
        {
            Id = 42,
            Nickname = "user",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rating = 1500,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var handler = new RecordUserPresenceHandler(context);
        var earlier = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);
        var later = earlier.AddMinutes(1);
        (await context.Users.AsNoTracking().SingleAsync()).LastSeenAt.Should().BeNull();

        await handler.Handle(new RecordUserPresenceCommand { UserId = 42, OccurredAt = later }, CancellationToken.None);
        await handler.Handle(new RecordUserPresenceCommand { UserId = 42, OccurredAt = earlier }, CancellationToken.None);

        (await context.Users.AsNoTracking().SingleAsync()).LastSeenAt.Should().Be(later);
    }

    [Fact]
    public async Task Rejects_non_utc_presence_time()
    {
        var (context, connection) = DbContextFactory.CreateSqliteContext();
        await using var _ = context;
        await using var __ = connection;

        var result = await new RecordUserPresenceHandler(context).Handle(
            new RecordUserPresenceCommand
            {
                UserId = 42,
                OccurredAt = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Unspecified)
            },
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }
}
