using Duely.Application.Services.Errors;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Duels;
using Duely.Application.UseCases.Helpers;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelsHistoryQuery : IRequest<Result<List<DuelDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetDuelsHistoryHandler(Context context, IRatingManager ratingManager, ITaskService taskService)
    : IRequestHandler<GetDuelsHistoryQuery, Result<List<DuelDto>>>
{
    public async Task<Result<List<DuelDto>>> Handle(GetDuelsHistoryQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }
        
        var duels = await context.Duels
            .AsNoTracking()
            .Where(d => d.Status == DuelStatus.Finished &&
                        (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(duel => duel.Configuration)
            .Include(duel => duel.User1)
            .Include(duel => duel.User2)
            .Include(duel => duel.Winner)
            .Include(duel => duel.Submissions)
            .ThenInclude(s => s.User)
            .OrderByDescending(d => d.StartTime)
            .ToListAsync(cancellationToken);

        return duels
            .Select(duel => DuelDtoMapper.Map(duel, query.UserId, ratingManager, taskService))
            .ToList();
    }
}
