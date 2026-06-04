using Duely.Application.UseCases.Dto.Duels;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class UpdateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required bool ShouldShowOpponentSolution { get; init; }
    public required int DurationMinutes { get; init; }
    public required int ProblemsCount { get; init; }
    public required ProblemsOrder ProblemsOrder { get; init; }
}

internal sealed class UpdateDuelConfigurationHandler(
    Context context,
    ILogger<UpdateDuelConfigurationHandler> logger)
    : IRequestHandler<UpdateDuelConfigurationCommand, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        UpdateDuelConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Nickname)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return new ForbiddenError();
        }
        
        var configuration = await context.DuelConfigurations
            .Include(c => c.CreatedBy)
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (configuration is null)
        {
            return new DuelConfigurationNotFoundError();
        }

        if (configuration.CreatedBy is not null && configuration.CreatedBy.Id != command.UserId)
        {
            return new ForbiddenError();
        }
        
        configuration.Update(
            command.ShouldShowOpponentSolution,
            command.DurationMinutes,
            command.ProblemsCount,
            command.ProblemsOrder);

        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} updated duel configuration {Id}", user.Nickname, configuration.Id);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentSolution = configuration.ShouldShowOpponentSolution,
            DurationMinutes = configuration.DurationMinutes,
            ProblemsCount = configuration.ProblemsCount,
            ProblemsOrder = configuration.ProblemsOrder
        };
    }
}

internal sealed class UpdateDuelConfigurationCommandValidator : AbstractValidator<UpdateDuelConfigurationCommand>
{
    public UpdateDuelConfigurationCommandValidator()
    {
        RuleFor(r => r.DurationMinutes)
            .GreaterThan(0).WithMessage("Длительность дуэли не может быть меньше либо равна 0.");
        
        RuleFor(r => r.DurationMinutes)
            .LessThanOrEqualTo(DuelConfiguration.MaxDurationMinutes)
            .WithMessage($"Длительность дуэли не может превышать {DuelConfiguration.MaxDurationMinutes} минут.");
        
        RuleFor(r => r.ProblemsCount)
            .GreaterThan(0).WithMessage("Дуэль должна содержать хотя бы одну задачу.");
        
        RuleFor(r => r.ProblemsCount)
            .LessThanOrEqualTo(DuelConfiguration.MaxProblemsCount)
            .WithMessage($"Дуэль не может содержать больше {DuelConfiguration.MaxProblemsCount} задач.");

        RuleFor(r => r.ProblemsOrder)
            .IsInEnum().WithMessage("Некорректное значение свойства, определяющего порядок выдачи задач.");
    }
}
