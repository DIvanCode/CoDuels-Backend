using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Helpers;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelsByStatusQuery : IRequest<Result<List<DuelDto>>>
{
    public required DuelStatus Status { get; init; }
}

public sealed class GetDuelsByStatusHandler(
    Context context,
    IRatingManager ratingManager,
    ITaskService taskService)
    : IRequestHandler<GetDuelsByStatusQuery, Result<List<DuelDto>>>
{
    public async Task<Result<List<DuelDto>>> Handle(
        GetDuelsByStatusQuery query,
        CancellationToken cancellationToken)
    {
        var duels = await context.Duels
            .AsNoTracking()
            .Where(duel => duel.Status == query.Status)
            .Include(duel => duel.Configuration)
            .Include(duel => duel.User1)
            .Include(duel => duel.User2)
            .Include(duel => duel.Winner)
            .Include(duel => duel.Submissions)
            .ThenInclude(submission => submission.User)
            .OrderByDescending(duel => duel.StartTime)
            .ToListAsync(cancellationToken);

        return duels
            .Select(duel => DuelDtoMapper.Map(duel, ratingManager, taskService))
            .ToList();
    }
}
