using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetActiveDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
}

public sealed class GetActiveDuelHandler(Context context, IRatingManager ratingManager, ITaskService taskService)
    : IRequestHandler<GetActiveDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetActiveDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .AsNoTracking()
            .Where(d =>
                d.Status == DuelStatus.InProgress &&
                (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(d => d.Configuration)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .Include(d => d.Submissions)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(User.Id), query.UserId);
        }

        return DuelDtoMapper.Map(duel, query.UserId, ratingManager, taskService);
    }
}
