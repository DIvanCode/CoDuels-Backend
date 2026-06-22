using Duely.Domain.Models.Users;
using FluentValidation;

namespace Duely.Application.Handlers.Users.Validators;

internal sealed class IdentityTicketValidator : AbstractValidator<string>
{
    public IdentityTicketValidator()
    {
        RuleFor(x => x)
            .MaximumLength(UserConstants.RefreshToken.MaxLength)
            .WithMessage("Слишком длинный обменный токен.");
    }
}
