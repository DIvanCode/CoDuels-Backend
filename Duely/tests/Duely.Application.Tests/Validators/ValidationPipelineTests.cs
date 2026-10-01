using Duely.Application.Tests.TestHelpers;
using Duely.Application.UseCases;
using Duely.Application.UseCases.Features.Duels;
using Duely.Application.UseCases.Features.UserActions;
using Duely.Application.UseCases.Features.Users;
using Duely.Domain.Models.Duels;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duely.Application.Tests.Validators;

public class ValidationPipelineTests : ContextBasedTest
{
    [Fact]
    public void Live_solution_validator_allows_clearing_the_editor()
    {
        var validator = new UpdateDuelTaskSolutionCommandValidator();
        var result = validator.Validate(new UpdateDuelTaskSolutionCommand
        {
            UserId = 1,
            DuelId = 2,
            TaskKey = 'A',
            Solution = string.Empty,
            Language = Language.Python
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Mediator_runs_registered_validators_before_the_handler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Context);
        services.SetupUseCases(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Func<Task> register = () => mediator.Send(new RegisterCommand
        {
            Nickname = "bad nickname",
            Password = "short"
        });
        var registerError = await register.Should().ThrowAsync<ValidationException>();
        registerError.Which.Errors.Should().Contain(error => error.PropertyName == "Nickname");
        registerError.Which.Errors.Should().Contain(error => error.PropertyName == "Password");
        Context.Users.Should().BeEmpty();

        Func<Task> getActions = () => mediator.Send(new GetUserActionsQuery
        {
            DuelId = 0,
            UserId = 0,
            TaskKey = default
        });
        var actionsError = await getActions.Should().ThrowAsync<ValidationException>();
        actionsError.Which.Errors.Select(error => error.PropertyName)
            .Should().BeEquivalentTo(["DuelId", "UserId", "TaskKey"]);
    }

    [Fact]
    public async Task Mediator_allows_valid_registration_with_existing_nickname_rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Context);
        services.SetupUseCases(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RegisterCommand
        {
            Nickname = "user-1",
            Password = "password123"
        });

        result.IsSuccess.Should().BeTrue();
        Context.Users.Should().ContainSingle(user => user.Nickname == "user-1");
    }
}
