using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class RegisterHandlerTests
{
    [Fact]
    public async Task AlreadyExists_when_nickname_dup()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        ctx.Users.Add(new User { Id = 1, Nickname = "alice", PasswordHash = "h", PasswordSalt = "s" });
        await ctx.SaveChangesAsync();

        var handler = new RegisterHandler(ctx);
        var res = await handler.Handle(new RegisterCommand { Nickname = "alice", Password = "x" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityAlreadyExistsError);
    }

    [Fact]
    public async Task Success_creates_user_with_hash_and_salt()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var handler = new RegisterHandler(ctx);
        var res = await handler.Handle(new RegisterCommand { Nickname = "bob", Password = "p@ss" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();

        var user = await ctx.Users.SingleAsync(u => u.Nickname == "bob");
        user.PasswordSalt.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        BCrypt.Net.BCrypt.Verify("p@ss" + user.PasswordSalt, user.PasswordHash).Should().BeTrue();
    }
}
