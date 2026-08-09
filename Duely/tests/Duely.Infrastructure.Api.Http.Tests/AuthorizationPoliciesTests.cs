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
        const string isAdminClaim = "admin_flag";
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{JwtTokenOptions.SectionName}:{nameof(JwtTokenOptions.IsAdminClaim)}"] = isAdminClaim
            })
            .Build();
        services.SetupApiHttp(
            configuration,
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
        claimRequirement.ClaimType.Should().Be(isAdminClaim);
        claimRequirement.AllowedValues.Should().Equal(bool.TrueString);
    }
}
