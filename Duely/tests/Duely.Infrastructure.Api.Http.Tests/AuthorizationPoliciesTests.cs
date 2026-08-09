using Duely.Domain.Services.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class AuthorizationPoliciesTests
{
    [Fact]
    public async Task OnlyAdmin_RequiresTrueIsAdminClaim()
    {
        var services = new ServiceCollection();
        services.SetupApiHttp(
            new ConfigurationBuilder().Build(),
            new Mock<IWebHostEnvironment>().Object);

        await using var serviceProvider = services.BuildServiceProvider();
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(AuthorizationPolicies.OnlyAdmin);

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().ContainSingle();
        var claimRequirement = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Should()
            .ContainSingle()
            .Subject;
        claimRequirement.ClaimType.Should().Be(UserClaims.IsAdmin);
        claimRequirement.AllowedValues.Should().Equal(bool.TrueString);
    }
}
