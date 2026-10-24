using Duely.Application.Tests.TestHelpers;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Tests.Persistence;

public sealed class UserNicknameIndexTests
{
    [Fact]
    public async Task Rejects_nicknames_that_differ_only_by_case()
    {
        var (context, connection) = DbContextFactory.CreateSqliteContext();
        await using var _ = connection;
        await using var __ = context;

        context.Users.Add(MakeUser(1, "DIvanCode"));
        await context.SaveChangesAsync();

        context.Users.Add(MakeUser(2, "divancode"));

        Func<Task> save = async () => await context.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    private static User MakeUser(int id, string nickname) => new()
    {
        Id = id,
        Nickname = nickname,
        PasswordHash = "hash",
        PasswordSalt = "salt",
        Rating = 1500,
        CreatedAt = DateTime.UtcNow
    };
}
