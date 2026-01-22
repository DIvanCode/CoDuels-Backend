using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Domain.Models;
using FluentAssertions;

namespace Duely.Application.Tests.Validators;

public class DuelConfigurationCommandValidatorTests
{
    [Fact]
    public void CreateValidator_Allows_sequential_letter_keys()
    {
        var command = new CreateDuelConfigurationCommand
        {
            UserId = 1,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] },
                ['B'] = new() { Level = 2, Topics = [] }
            }
        };

        var result = new CreateDuelConfigurationCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidator_Rejects_numeric_keys()
    {
        var command = new CreateDuelConfigurationCommand
        {
            UserId = 1,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['1'] = new() { Level = 1, Topics = [] },
                ['2'] = new() { Level = 2, Topics = [] }
            }
        };

        var result = new CreateDuelConfigurationCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateValidator_Rejects_non_sequential_letter_keys()
    {
        var command = new UpdateDuelConfigurationCommand
        {
            Id = 10,
            UserId = 1,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] },
                ['C'] = new() { Level = 2, Topics = [] }
            }
        };

        var result = new UpdateDuelConfigurationCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
