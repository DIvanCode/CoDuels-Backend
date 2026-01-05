using Duely.Domain.Models;
using Duely.Domain.Services.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace Duely.Domain.Tests;

public class TokenServiceTests
{
    private static readonly IOptions<JwtTokenOptions> TestOptions = new OptionsWrapper<JwtTokenOptions>(new JwtTokenOptions
    {
        SecretKey = "test-secret-key-that-is-long-enough-for-hmac-sha256-algorithm",
        IdClaim = "user_id",
        ExpiresHours = 24
    });

    [Fact]
    public void GenerateTokens_ReturnsAccessAndRefreshTokens()
    {
        var service = new TokenService(TestOptions);
        var user = new User
        {
            Id = 1,
            Nickname = "testuser",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        var (accessToken, refreshToken) = service.GenerateTokens(user);

        accessToken.Should().NotBeNullOrEmpty();
        refreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateTokens_AccessTokenContainsUserId()
    {
        var service = new TokenService(TestOptions);
        var user = new User
        {
            Id = 42,
            Nickname = "testuser",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        var (accessToken, _) = service.GenerateTokens(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(accessToken);
        
        token.Claims.Should().Contain(c => c.Type == "user_id" && c.Value == "42");
    }

    [Fact]
    public void GenerateTokens_AccessTokenHasExpiration()
    {
        var service = new TokenService(TestOptions);
        var user = new User
        {
            Id = 1,
            Nickname = "testuser",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        var (accessToken, _) = service.GenerateTokens(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(accessToken);
        
        token.ValidTo.Should().BeAfter(DateTime.UtcNow);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateTokens_RefreshTokenIsGuid()
    {
        var service = new TokenService(TestOptions);
        var user = new User
        {
            Id = 1,
            Nickname = "testuser",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        var (_, refreshToken) = service.GenerateTokens(user);

        Guid.TryParse(refreshToken, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateTokens_DifferentCallsReturnDifferentRefreshTokens()
    {
        var service = new TokenService(TestOptions);
        var user = new User
        {
            Id = 1,
            Nickname = "testuser",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = DateTime.UtcNow
        };

        var (_, refresh1) = service.GenerateTokens(user);
        var (_, refresh2) = service.GenerateTokens(user);

        // Refresh tokens are always different (GUID)
        refresh1.Should().NotBe(refresh2);
    }
}

