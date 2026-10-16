using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetActiveDuelIdQuery : IRequest<ActiveDuelIdDto>
{
    public required int UserId { get; init; }
}

public sealed class GetActiveDuelIdHandler(Context context)
    : IRequestHandler<GetActiveDuelIdQuery, ActiveDuelIdDto>
{
    public async Task<ActiveDuelIdDto> Handle(
        GetActiveDuelIdQuery query,
        CancellationToken cancellationToken)
    {
        var duelId = await context.Duels
            .AsNoTracking()
            .Where(duel => duel.Status == DuelStatus.InProgress &&
                (duel.User1.Id == query.UserId || duel.User2.Id == query.UserId))
            .OrderByDescending(duel => duel.StartTime)
            .ThenByDescending(duel => duel.Id)
            .Select(duel => (int?)duel.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new ActiveDuelIdDto { DuelId = duelId };
    }
}
