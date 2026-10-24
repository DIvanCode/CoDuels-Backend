using System.Data.Common;
using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using Duely.Domain.Services.Users;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class LoginHandlerTests : IAsyncLifetime
{
    private readonly DbConnection _connection;
    private Context Context { get; }

    public LoginHandlerTests()
    {
        (Context, _connection) = DbContextFactory.CreateSqliteContext();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    (string Salt, string Hash) Make(string pwd) {
        var salt = "pepper";
        var hash = BCrypt.Net.BCrypt.HashPassword(pwd + salt);
        return (salt, hash);
    }

    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var ctx = Context;
        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance);

        var res = await handler.Handle(new LoginCommand { Nickname = "ghost", Password = "x" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        tokenSvc.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthError_when_password_wrong()
    {
        var ctx = Context;
        var (salt, hash) = Make("correct");
        ctx.Users.Add(new User { Id = 1, Nickname = "neo", PasswordSalt = salt, PasswordHash = hash, Rating = 0, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance);

        var res = await handler.Handle(new LoginCommand { Nickname = "neo", Password = "wrong" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is AuthenticationError);
        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 1);
        user.RefreshToken.Should().BeNull();
        user.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public async Task Success_returns_tokens_and_persists_refresh()
    {
        var ctx = Context;
        var (salt, hash) = Make("secret");
        ctx.Users.Add(new User { Id = 2, Nickname = "Trinity", PasswordSalt = salt, PasswordHash = hash, Rating = 0, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)))
            .Returns(("ACCESS", "REFRESH")).Verifiable();

        var handler = new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance);
        var beforeLogin = DateTime.UtcNow;
        var res = await handler.Handle(new LoginCommand { Nickname = "TRINITY", Password = "secret" }, CancellationToken.None);
        var afterLogin = DateTime.UtcNow;

        res.IsSuccess.Should().BeTrue();
        res.Value.AccessToken.Should().Be("ACCESS");
        res.Value.RefreshToken.Should().Be("REFRESH");
        tokenSvc.Verify();

        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 2);
        user.Nickname.Should().Be("Trinity");
        user.RefreshToken.Should().Be("REFRESH");
        user.LastSeenAt.Should().BeOnOrAfter(beforeLogin).And.BeOnOrBefore(afterLogin);
    }

    [Fact]
    public async Task Success_does_not_replace_newer_presence_time()
    {
        var ctx = Context;
        var (salt, hash) = Make("secret");
        var laterPresence = DateTime.UtcNow.AddMinutes(5);
        ctx.Users.Add(new User
        {
            Id = 3,
            Nickname = "morpheus",
            PasswordSalt = salt,
            PasswordHash = hash,
            Rating = 0,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = laterPresence
        });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.IsAny<User>())).Returns(("ACCESS", "REFRESH"));

        var result = await new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance)
            .Handle(new LoginCommand { Nickname = "morpheus", Password = "secret" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 3);
        user.LastSeenAt.Should().Be(laterPresence);
        user.RefreshToken.Should().Be("REFRESH");
    }
}
