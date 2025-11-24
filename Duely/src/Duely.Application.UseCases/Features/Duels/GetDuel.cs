using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class GetDuelHandler(Context context) : IRequestHandler<GetDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Id == query.DuelId)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }

        var duelDto = new DuelDto
        {
            Id = duel.Id,
            TaskId = duel.TaskId,
            OpponentId = duel.User1.Id == query.UserId ? duel.User2.Id : duel.User1.Id,
            Status = duel.Status,
            StartTime = duel.StartTime,
            DeadlineTime = duel.DeadlineTime,
        };

        if (duel.EndTime is not null)
        {
            duelDto.EndTime = duel.EndTime;
            if (duel.Winner is null)
            {
                duelDto.Result = DuelResult.Draw;
            }
            else if (duel.Winner.Id == query.UserId)
            {
                duelDto.Result = DuelResult.Win;
            }
            else
            {
                duelDto.Result = DuelResult.Lose;
            }
        }

        return duelDto;
    }
}