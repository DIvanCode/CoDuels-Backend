using Duely.Infrastructure.Api.Http.Requests.Users;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.Users;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("Nickname is required.")
            .MaximumLength(1000).WithMessage("Nickname is too long.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(1000).WithMessage("Password is too long.");
    }
}