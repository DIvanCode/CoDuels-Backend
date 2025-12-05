using Duely.Infrastructure.Api.Http.Requests.UserCodeRuns;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.UserCodeRuns;

public class RunUserCodeValidator : AbstractValidator<RunUserCodeRequest>
{
    public RunUserCodeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required.")
            .Must(lang => SupportedLanguages.All.Contains(lang)).WithMessage("Unsupported language. Allowed: C++, Python, Golang.");

        RuleFor(x => x.Input).MaximumLength(10000).WithMessage("Input is too long.");
    }
}