using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class UpdateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
    public required bool ShouldShowOpponentSolution { get; init; }
    public required int MaxDurationMinutes { get; init; }
    public required int TasksCount { get; init; }
    public required DuelTasksOrder TasksOrder { get; init; }
    public required Dictionary<char, DuelTaskConfiguration> TasksConfigurations { get; init; }
}

public sealed class UpdateDuelConfigurationHandler(Context context)
    : IRequestHandler<UpdateDuelConfigurationCommand, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        UpdateDuelConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations
            .Include(c => c.Owner)
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

        if (configuration.Owner?.Id != request.UserId)
        {
            return new ForbiddenError(nameof(DuelConfiguration), "update", nameof(DuelConfiguration.Id), request.Id);
        }
        
        configuration.ShouldShowOpponentSolution = request.ShouldShowOpponentSolution;
        configuration.MaxDurationMinutes = request.MaxDurationMinutes;
        configuration.TasksCount = request.TasksCount;
        configuration.TasksOrder = request.TasksOrder;
        configuration.TasksConfigurations = request.TasksConfigurations;

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

public sealed class UpdateDuelConfigurationCommandValidator : AbstractValidator<UpdateDuelConfigurationCommand>
{
    public UpdateDuelConfigurationCommandValidator()
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
