using Duely.Infrastructure.Api.Http.Requests.Users;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.Users;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("Nickname is required.")
            .MinimumLength(4).WithMessage("Nickname is too short.")
            .MaximumLength(32).WithMessage("Nickname is too long.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password is too short.")
            .MaximumLength(64).WithMessage("Password is too long.");
    }
}