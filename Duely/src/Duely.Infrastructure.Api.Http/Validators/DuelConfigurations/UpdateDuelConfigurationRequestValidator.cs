using Duely.Infrastructure.Api.Http.Requests.DuelConfigurations;
using FluentValidation;

namespace Duely.Infrastructure.Api.Http.Validators.DuelConfigurations;

public sealed class UpdateDuelConfigurationRequestValidator : AbstractValidator<UpdateDuelConfigurationRequest>
{
    public UpdateDuelConfigurationRequestValidator()
    {
        RuleFor(r => r.MaxDurationMinutes)
            .GreaterThanOrEqualTo(5).WithMessage("max duration minutes must be greater than or equal to 5")
            .LessThanOrEqualTo(300).WithMessage("max duration minutes must be less than or equal to 300");

        RuleFor(r => r.TasksCount)
            .GreaterThan(0).WithMessage("task count must be greater than 0")
            .LessThanOrEqualTo(10).WithMessage("task count must be less than or equal to 10");

        RuleFor(r => r.TasksOrder)
            .IsInEnum().WithMessage("tasks order has invalid value");
        
        RuleFor(r => r.TasksConfigurations)
            .NotEmpty().WithMessage("configuration for each task is required");

        RuleFor(r => r)
            .Must(r => r.TasksCount == r.TasksConfigurations.Count)
            .WithMessage("tasks count must match tasks configurations length")
            .Must(r => r.TasksConfigurations.Keys.Distinct().Count() == r.TasksCount)
            .WithMessage("tasks configurations must have unique keys");
        
        RuleForEach(r => r.TasksConfigurations).ChildRules(configuration =>
        {
            configuration.RuleFor(c => c.Value.Level)
                .GreaterThanOrEqualTo(1).WithMessage("task level must be greater than or equal to 1")
                .LessThanOrEqualTo(10).WithMessage("task level must be less than or equal to 10");
        });
    }
}

