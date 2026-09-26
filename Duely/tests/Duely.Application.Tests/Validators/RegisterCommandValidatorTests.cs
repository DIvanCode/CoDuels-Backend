using Duely.Application.UseCases.Features.Users;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    [Theory]
    [InlineData("user_1")]
    [InlineData("AZaz09_")]
    public void Accepts_valid_command(string nickname)
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand
        {
            Nickname = nickname,
            Password = "password123"
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("bad nickname")]
    [InlineData("bad-name")]
    [InlineData("имя")]
    [InlineData("bad.name")]
    [InlineData("user\n")]
    [InlineData("")]
    public void Rejects_invalid_nickname(string nickname)
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand
        {
            Nickname = nickname,
            Password = "password123"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nickname");
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
