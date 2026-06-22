using Duely.Domain.Models.Users;
using FluentValidation;

namespace Duely.Application.Handlers.Users.Validators;

internal sealed class NicknameValidator : AbstractValidator<string>
{
    public NicknameValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Никнейм не может быть пустым.");
        RuleFor(x => x)
            .MaximumLength(UserConstants.Nickname.MaxLength)
            .WithMessage($"Никнейм не может содержать более {UserConstants.Nickname.MaxLength} символов.");
        RuleFor(x => x)
            .Matches(UserConstants.Nickname.Regex)
            .WithMessage("Никнейм содержит недопустимые символы.");
    }
}
