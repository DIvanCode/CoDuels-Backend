using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
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

        if (configuration.Owner?.Id != request.UserId || configuration.IsDeleted)
        {
            return new ForbiddenError(nameof(DuelConfiguration), "update", nameof(DuelConfiguration.Id), request.Id);
        }
        
        configuration.ShouldShowOpponentSolution = request.ShouldShowOpponentSolution;
        configuration.MaxDurationMinutes = request.MaxDurationMinutes;
        configuration.ProblemsCount = request.TasksCount;
        configuration.ProblemsOrder = request.TasksOrder;

        await context.SaveChangesAsync(cancellationToken);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            IsDeleted = configuration.IsDeleted,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
            MaxDurationMinutes = configuration.MaxDurationMinutes,
            TasksCount = configuration.ProblemsCount,
            TasksOrder = configuration.ProblemsOrder
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
    }
}
