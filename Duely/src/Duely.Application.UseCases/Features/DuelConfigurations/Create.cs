using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed class CreateDuelConfigurationCommand : IRequest<Result<DuelConfigurationDto>>
{
    public required int UserId { get; init; }
    public required bool ShouldShowOpponentCode { get; init; }
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
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == request.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), request.UserId);
        }
        
        var configuration = new DuelConfiguration
        {
            Owner = user,
            IsRated = false,
            ShouldShowOpponentCode = request.ShouldShowOpponentCode,
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

