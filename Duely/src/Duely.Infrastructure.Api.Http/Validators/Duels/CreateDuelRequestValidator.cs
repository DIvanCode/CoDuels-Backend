using Duely.Infrastructure.Api.Http.Requests.Duels;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.Duels;

public sealed class CreateDuelRequestValidator : AbstractValidator<CreateDuelRequest>
{
    public CreateDuelRequestValidator()
    {
        RuleFor(r => r.OpponentNickname).NotEmpty().MaximumLength(64);
        RuleFor(r => r.ConfigurationId).GreaterThan(0);
    }
}
