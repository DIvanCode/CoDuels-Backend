using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetActiveDuelIdsQuery : IRequest<ActiveDuelIdsDto>
{
    public required int UserId { get; init; }
}

public sealed class GetActiveDuelIdsHandler(Context context)
    : IRequestHandler<GetActiveDuelIdsQuery, ActiveDuelIdsDto>
{
    public async Task<ActiveDuelIdsDto> Handle(
        GetActiveDuelIdsQuery query,
        CancellationToken cancellationToken)
    {
        var duelIds = await context.Duels
            .AsNoTracking()
            .Where(duel => duel.Status == DuelStatus.InProgress &&
                (duel.User1.Id == query.UserId || duel.User2.Id == query.UserId))
            .OrderByDescending(duel => duel.StartTime)
            .ThenByDescending(duel => duel.Id)
            .Select(duel => duel.Id)
            .ToListAsync(cancellationToken);

        return new ActiveDuelIdsDto { DuelIds = duelIds };
    }
}
