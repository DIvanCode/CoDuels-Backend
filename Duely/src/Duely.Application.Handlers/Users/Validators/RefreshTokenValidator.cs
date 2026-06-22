using Duely.Domain.Models.Users;
using FluentValidation;

namespace Duely.Application.Handlers.Users.Validators;

internal sealed class RefreshTokenValidator : AbstractValidator<string>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x)
            .MaximumLength(UserConstants.IdentityTicket.MaxLength)
            .WithMessage("Слишком длинный идентификационный билет.");
    }
}
