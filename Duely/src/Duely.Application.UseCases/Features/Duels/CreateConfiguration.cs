using Duely.Application.UseCases.Dto.Duels;
using Duely.Domain.Common.Errors;
using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CreateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required Guid UserId { get; init; }
    public required bool ShouldShowOpponentSolution { get; init; }
    public required int DurationMinutes { get; init; }
    public required int ProblemsCount { get; init; }
    public required ProblemsOrder ProblemsOrder { get; init; }
}

internal sealed class CreateDuelConfigurationHandler(
    Context context,
    ILogger<CreateDuelConfigurationHandler> logger)
    : IRequestHandler<CreateDuelConfigurationCommand, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        CreateDuelConfigurationCommand command,
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
        
        var id = new DuelConfigurationId(Guid.NewGuid());
        var configuration = new DuelConfiguration(
            id,
            isRated: false,
            command.ShouldShowOpponentSolution,
            command.DurationMinutes,
            command.ProblemsCount,
            command.ProblemsOrder,
            user);

        context.DuelConfigurations.Add(configuration);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("User {Nickname} created duel configuration {Id}", user.Nickname, id);

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

internal sealed class CreateDuelConfigurationCommandValidator : AbstractValidator<CreateDuelConfigurationCommand>
{
    public CreateDuelConfigurationCommandValidator()
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
