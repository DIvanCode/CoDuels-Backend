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
    public async Task NotFound_when_refresh_token_unknown()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        // Никаких пользователей в БД с таким токеном нет
        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new RefreshHandler(ctx, tokenSvc.Object);

        var res = await handler.Handle(new RefreshCommand { RefreshToken = "NOPE" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);

        // Убеждаемся, что генерация токенов не вызывалась
        tokenSvc.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Success_generates_and_persists_new_refresh_by_token_lookup()
    {
        var (ctx, conn) = DbContextFactory.CreateSqliteContext(); await using var _ = conn;

        var user = new User { Id = 2, Nickname = "trinity", PasswordHash = "h", PasswordSalt = "s", RefreshToken = "OLD" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)))
                .Returns(("ACCESS2", "REFRESH2"));

        var handler = new RefreshHandler(ctx, tokenSvc.Object);

        var res = await handler.Handle(new RefreshCommand { RefreshToken = "OLD" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.AccessToken.Should().Be("ACCESS2");
        res.Value.RefreshToken.Should().Be("REFRESH2");

        var refreshed = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 2);
        refreshed.RefreshToken.Should().Be("REFRESH2");

        tokenSvc.Verify(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)), Times.Once);
        tokenSvc.VerifyNoOtherCalls();
    }
}
