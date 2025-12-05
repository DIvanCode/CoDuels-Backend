using Duely.Infrastructure.Api.Http.Requests.Submissions;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.Submissions;

public class SendRequestValidator : AbstractValidator<SendSubmissionRequest>
{ 

    public SendRequestValidator()
    {
        RuleFor(x => x.Submission).NotEmpty().WithMessage("Submission is required.");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required.")
            .Must(lang => SupportedLanguages.All.Contains(lang)).WithMessage("Unsupported language. Allowed: C++, Python, Golang.");
    }
}