using Duely.Domain.Models.Users;
using FluentValidation;

namespace Duely.Application.Handlers.Users.Validators;

internal sealed class PasswordValidator : AbstractValidator<string>
{
    public PasswordValidator()
    {
        RuleFor(x => x)
            .MinimumLength(UserConstants.Password.MinLength)
            .WithMessage($"Пароль должен содержать не менее {UserConstants.Password.MinLength} символов.");
        RuleFor(x => x)
            .MaximumLength(UserConstants.Password.MaxLength)
            .WithMessage($"Пароль не может содержать более {UserConstants.Password.MaxLength} символов.");
    }
}
