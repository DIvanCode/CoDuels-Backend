using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.DuelConfigurations;

public sealed record GetDuelConfigurationQuery(int Id) : IRequest<Result<DuelConfigurationDto>>;

public sealed class GetDuelConfigurationHandler(Context context)
    : IRequestHandler<GetDuelConfigurationQuery, Result<DuelConfigurationDto>>
{
    public async Task<Result<DuelConfigurationDto>> Handle(
        GetDuelConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        var configuration = await context.DuelConfigurations.SingleOrDefaultAsync(
            c => c.Id == request.Id,
            cancellationToken);
        if (configuration is null)
        {
            return new EntityNotFoundError(nameof(DuelConfiguration), nameof(DuelConfiguration.Id), request.Id);
        }

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

