using Duely.Infrastructure.Api.Http.Requests.Users;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.Users;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required.");
    }
}