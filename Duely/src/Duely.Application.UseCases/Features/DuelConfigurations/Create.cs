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
            TasksOrder = request.TasksOrder
        };

        context.DuelConfigurations.Add(configuration);
        await context.SaveChangesAsync(cancellationToken);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
            MaxDurationMinutes = configuration.MaxDurationMinutes,
            TasksCount = configuration.TasksCount,
            TasksOrder = configuration.TasksOrder
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
    }
}
