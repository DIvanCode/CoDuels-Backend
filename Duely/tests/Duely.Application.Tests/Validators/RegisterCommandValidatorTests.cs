using Duely.Application.UseCases.Features.Users;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    [Fact]
    public void Accepts_valid_command()
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand
        {
            Nickname = "user_1",
            Password = "password123"
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_invalid_nickname()
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand
        {
            Nickname = "bad nickname",
            Password = "password123"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Nickname");
    }

    [Fact]
    public void Rejects_short_password()
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand
        {
            Nickname = "user_1",
            Password = "short"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
    }
}
