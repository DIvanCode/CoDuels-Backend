using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class CreateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required int UserId { get; init; }
    public required bool ShouldShowOpponentSolution { get; init; }
    public required int MaxDurationMinutes { get; init; }
    public required int TasksCount { get; init; }
    public required DuelTasksOrder TasksOrder { get; init; }
    public required Dictionary<char, DuelTaskConfiguration> TasksConfigurations { get; init; }
}

public sealed class CreateDuelConfigurationHandler(Context context)
    : IRequestHandler<CreateDuelConfigurationCommand, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        CreateDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), request.UserId);
        }
        
        var configuration = new DuelConfiguration
        {
            Owner = user,
            IsRated = false,
            ShouldShowOpponentSolution = request.ShouldShowOpponentSolution,
            MaxDurationMinutes = request.MaxDurationMinutes,
            TasksCount = request.TasksCount,
            TasksOrder = request.TasksOrder,
            TasksConfigurations = request.TasksConfigurations
        };

        context.DuelConfigurations.Add(configuration);
        await context.SaveChangesAsync(cancellationToken);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
            MaxDurationMinutes = configuration.MaxDurationMinutes,
            TasksCount = configuration.TasksCount,
            TasksOrder = configuration.TasksOrder,
            Tasks = configuration.TasksConfigurations.ToDictionary(
                task => task.Key,
                task => new DuelTaskConfigurationDto
                {
                    Level = task.Value.Level,
                    Topics = task.Value.Topics
                })
        };
    }
}

public sealed class CreateDuelConfigurationCommandValidator : AbstractValidator<CreateDuelConfigurationCommand>
{
    public CreateDuelConfigurationCommandValidator()
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
            .WithMessage("tasks configurations must have unique keys")
            .Must(r => HasSequentialAlphabeticKeys(r.TasksConfigurations, r.TasksCount))
            .WithMessage("tasks configurations must use sequential A.. keys");

        RuleForEach(r => r.TasksConfigurations).ChildRules(configuration =>
        {
            configuration.RuleFor(c => c.Value.Level)
                .GreaterThanOrEqualTo(1).WithMessage("task level must be greater than or equal to 1")
                .LessThanOrEqualTo(10).WithMessage("task level must be less than or equal to 10");
        });
    }

    private static bool HasSequentialAlphabeticKeys(
        IReadOnlyDictionary<char, DuelTaskConfiguration> configurations,
        int tasksCount)
    {
        if (tasksCount <= 0 || configurations.Count != tasksCount)
        {
            return false;
        }

        var expectedKeys = Enumerable.Range(0, tasksCount)
            .Select(i => (char)('A' + i))
            .ToHashSet();
        return configurations.Keys.All(expectedKeys.Contains) && configurations.Keys.Count() == expectedKeys.Count;
    }
}
