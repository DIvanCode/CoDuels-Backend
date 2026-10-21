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

    [Fact]
    public void Accepts_global_tournament_with_future_utc_start()
    {
        var result = _validator.Validate(new CreateTournamentCommand
        {
            UserId = 1,
            IsAdmin = true,
            Name = "World Cup",
            Description = "Open tournament",
            StartsAt = DateTime.UtcNow.AddHours(1),
            MatchmakingType = TournamentMatchmakingType.Swiss
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_global_tournament_without_description_or_future_start()
    {
        var result = _validator.Validate(new CreateTournamentCommand
        {
            UserId = 1,
            IsAdmin = true,
            Name = "World Cup",
            StartsAt = DateTime.UtcNow.AddHours(-1),
            MatchmakingType = TournamentMatchmakingType.Swiss
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain(["Description", "StartsAt"]);
    }

    [Fact]
    public void Rejects_swiss_matchmaking_in_group_tournament()
    {
        var result = _validator.Validate(new CreateTournamentCommand
        {
            UserId = 1,
            Name = "Cup",
            GroupId = 10,
            MatchmakingType = TournamentMatchmakingType.Swiss,
            Participants = ["u1", "u2"]
        });

        result.IsValid.Should().BeFalse();
    }
}
