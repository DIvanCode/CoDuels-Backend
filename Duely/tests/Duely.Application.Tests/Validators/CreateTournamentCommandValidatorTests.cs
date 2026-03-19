using Duely.Application.UseCases.Features.Tournaments;
using Duely.Domain.Models.Tournaments;
using FluentAssertions;

namespace Duely.Application.Tests.Validators;

public sealed class CreateTournamentCommandValidatorTests
{
    private readonly CreateTournamentCommandValidator _validator = new();

    [Fact]
    public void Accepts_valid_command()
    {
        var command = new CreateTournamentCommand
        {
            UserId = 1,
            Name = "Cup",
            GroupId = 10,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = ["u1", "u2"]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_less_than_two_participants()
    {
        var command = new CreateTournamentCommand
        {
            UserId = 1,
            Name = "Cup",
            GroupId = 10,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = ["u1"]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_duplicate_participants()
    {
        var command = new CreateTournamentCommand
        {
            UserId = 1,
            Name = "Cup",
            GroupId = 10,
            MatchmakingType = TournamentMatchmakingType.SingleEliminationBracket,
            Participants = ["u1", "u1"]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
