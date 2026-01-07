using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class UpdateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
    public required bool ShouldShowOpponentCode { get; init; }
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
        
        configuration.ShouldShowOpponentCode = request.ShouldShowOpponentCode;
        configuration.MaxDurationMinutes = request.MaxDurationMinutes;
        configuration.TasksCount = request.TasksCount;
        configuration.TasksOrder = request.TasksOrder;
        configuration.TasksConfigurations = request.TasksConfigurations;

        await context.SaveChangesAsync(cancellationToken);

        return new DuelConfigurationDto
        {
            Id = configuration.Id,
            ShouldShowOpponentCode = configuration.ShouldShowOpponentCode,
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

