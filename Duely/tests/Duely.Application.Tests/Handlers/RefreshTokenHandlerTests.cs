using Duely.Application.Services.Errors;
using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models;
using Duely.Domain.Services.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Duely.Application.Tests.Handlers;

public class RefreshTokenHandlerTests : ContextBasedTest
{
    [Fact]
    public async Task NotFound_when_refresh_token_unknown()
    {
        var ctx = Context;

        var tokenSvc = new Mock<ITokenService>(MockBehavior.Strict);
        var handler = new RefreshTokenHandler(ctx, tokenSvc.Object, NullLogger<RefreshTokenHandler>.Instance);

        var res = await handler.Handle(new RefreshTokenCommand { RefreshToken = "NOPE" }, CancellationToken.None);

        res.IsFailed.Should().BeTrue();
        res.Errors.Should().ContainSingle(e => e is EntityNotFoundError);
        tokenSvc.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Success_generates_and_persists_new_refresh_by_token_lookup()
    {
        var ctx = Context;

        var user = new User
        {
            Id = 2,
            Nickname = "trinity",
            PasswordHash = "h",
            PasswordSalt = "s",
            RefreshToken = "OLD",
            Rating = 0,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)))
            .Returns(("ACCESS2", "REFRESH2"));

        var handler = new RefreshTokenHandler(ctx, tokenSvc.Object, NullLogger<RefreshTokenHandler>.Instance);

        var res = await handler.Handle(new RefreshTokenCommand { RefreshToken = "OLD" }, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.AccessToken.Should().Be("ACCESS2");
        res.Value.RefreshToken.Should().Be("REFRESH2");

        var refreshed = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == 2);
        refreshed.RefreshToken.Should().Be("REFRESH2");

        tokenSvc.Verify(s => s.GenerateTokens(It.Is<User>(u => u.Id == 2)), Times.Once);
        tokenSvc.VerifyNoOtherCalls();
    }
}