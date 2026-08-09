using System.Security.Claims;
using Duely.Domain.Services.Users;
using Duely.Infrastructure.Api.Http.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class UserContextTests
{
    private static readonly IOptions<JwtTokenOptions> TestOptions = Options.Create(new JwtTokenOptions
    {
        SecretKey = "test-secret-key",
        IdClaim = "user_id"
    });

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("not-a-boolean", false)]
    public void IsAdmin_ReturnsExpectedValue(string claimValue, bool expected)
    {
        var context = CreateUserContext(new Claim(UserClaims.IsAdmin, claimValue));

        context.IsAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenClaimIsMissing()
    {
        var context = CreateUserContext();

        context.IsAdmin().Should().BeFalse();
    }

    private static UserContext CreateUserContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims))
        };

        return new UserContext(new HttpContextAccessor { HttpContext = httpContext }, TestOptions);
    }
}
