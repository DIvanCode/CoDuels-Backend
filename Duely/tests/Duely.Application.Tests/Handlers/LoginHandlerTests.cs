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
using Microsoft.Extensions.Logging.Abstractions;

public class LoginHandlerTests : ContextBasedTest
{
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
        ctx.Users.Add(new User { Id = 1, Nickname = "neo", PasswordSalt = salt, PasswordHash = hash, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance);

        var res = await handler.Handle(new LoginCommand { Nickname = "neo", Password = "wrong" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is AuthenticationError);
        (await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 1)).RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Success_returns_tokens_and_persists_refresh()
    {
        var ctx = Context;
        var (salt, hash) = Make("secret");
        ctx.Users.Add(new User { Id = 2, Nickname = "trinity", PasswordSalt = salt, PasswordHash = hash, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)))
                .Returns(("ACCESS", "REFRESH")).Verifiable();

        var handler = new LoginHandler(ctx, tokenSvc.Object, NullLogger<LoginHandler>.Instance);
        var res = await handler.Handle(new LoginCommand { Nickname = "trinity", Password = "secret" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.AccessToken.Should().Be("ACCESS");
        res.Value.RefreshToken.Should().Be("REFRESH");
        tokenSvc.Verify();

        (await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 2)).RefreshToken.Should().Be("REFRESH");
    }
}
