using System.Reflection;
using Duely.Infrastructure.Api.Http.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Duely.Infrastructure.Api.Http.Tests;

public sealed class AdminControllerAuthorizationTests
{
    public static TheoryData<Type, string> AdminActions => new()
    {
        { typeof(UsersController), nameof(UsersController.GetAllForAdminAsync) },
        { typeof(UsersController), nameof(UsersController.GetActiveForAdminAsync) },
        { typeof(DuelsController), nameof(DuelsController.GetPendingForAdminAsync) },
        { typeof(DuelsController), nameof(DuelsController.GetRankedSearchersForAdminAsync) },
        { typeof(DuelsController), nameof(DuelsController.GetAllActiveForAdminAsync) },
        { typeof(DuelsController), nameof(DuelsController.GetAllFinishedForAdminAsync) },
        { typeof(SubmissionsController), nameof(SubmissionsController.GetTestingForAdminAsync) },
        { typeof(SubmissionsController), nameof(SubmissionsController.GetAllForAdminAsync) },
        { typeof(GroupsController), nameof(GroupsController.GetAllForAdminAsync) },
        { typeof(TournamentsController), nameof(TournamentsController.GetActiveForAdminAsync) },
        { typeof(TournamentsController), nameof(TournamentsController.GetFinishedForAdminAsync) }
    };

    [Theory]
    [MemberData(nameof(AdminActions))]
    public void Admin_action_requires_only_admin_policy(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        method!.GetCustomAttributes<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => attribute.Policy == AuthorizationPolicies.OnlyAdmin);
    }
}
