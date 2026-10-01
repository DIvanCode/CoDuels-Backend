using Duely.Application.UseCases.Features.DuelConfigurations;
using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Tournaments;
using FluentAssertions;

namespace Duely.Application.Tests.Validators;

public sealed class ValidatorCompatibilityTests
{
    [Fact]
    public void Configuration_validators_report_missing_task_data_without_throwing()
    {
        var create = new CreateDuelConfigurationCommand
        {
            UserId = 1,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = null!
        };
        var update = new UpdateDuelConfigurationCommand
        {
            Id = 1,
            UserId = 1,
            ShouldShowOpponentSolution = true,
            MaxDurationMinutes = 30,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration> { ['A'] = null! }
        };

        new CreateDuelConfigurationCommandValidator().Validate(create).IsValid.Should().BeFalse();
        var result = new UpdateDuelConfigurationCommandValidator().Validate(update);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "task configuration is required");
    }

    [Fact]
    public void Tournament_validator_reports_missing_participants_without_throwing()
    {
        var command = new CreateTournamentCommand
        {
            UserId = 1,
            Name = "Cup",
            GroupId = 2,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = null!
        };

        new CreateTournamentCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }
}
