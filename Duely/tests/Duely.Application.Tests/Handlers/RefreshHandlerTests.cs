using System.Threading;
using System.Threading.Tasks;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using Duely.Domain.Services.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class RefreshHandlerTests
{
    [Fact]
    public async Task NotFound_when_user_absent()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new RefreshHandler(ctx, tokenSvc.Object);

        var res = await handler.Handle(new RefreshCommand { UserId = 1, RefreshToken = "X" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
    }

    [Fact]
    public async Task AuthError_when_token_mismatch()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        ctx.Users.Add(new User { Id = 1, Nickname = "neo", PasswordHash = "h", PasswordSalt = "s", RefreshToken = "OLD" });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new RefreshHandler(ctx, tokenSvc.Object);

        var res = await handler.Handle(new RefreshCommand { UserId = 1, RefreshToken = "WRONG" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is AuthenticationError);

        (await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 1)).RefreshToken.Should().Be("OLD");
    }

    [Fact]
    public async Task Success_generates_and_persists_new_refresh()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;
        ctx.Users.Add(new User { Id = 2, Nickname = "trinity", PasswordHash = "h", PasswordSalt = "s", RefreshToken = "OLD" });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)))
                .Returns(("ACCESS2", "REFRESH2"));

        var handler = new RefreshHandler(ctx, tokenSvc.Object);
        var res = await handler.Handle(new RefreshCommand { UserId = 2, RefreshToken = "OLD" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.AccessToken.Should().Be("ACCESS2");
        res.Value.RefreshToken.Should().Be("REFRESH2");

        (await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 2)).RefreshToken.Should().Be("REFRESH2");
    }
}
