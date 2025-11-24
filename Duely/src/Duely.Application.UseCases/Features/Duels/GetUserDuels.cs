using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetUserDuelsQuery : IRequest<Result<List<DuelHistoryItemDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetUserDuelsHandler(Context context)
    : IRequestHandler<GetUserDuelsQuery, Result<List<DuelHistoryItemDto>>>
{
    public async Task<Result<List<DuelHistoryItemDto>>> Handle(GetUserDuelsQuery query, CancellationToken cancellationToken)
    {
        var userExists = await context.Users.AnyAsync(u => u.Id == query.UserId, cancellationToken);

        if (!userExists)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }
        var duels = await context.Duels
            .Where(d => d.Status == DuelStatus.Finished &&
                ((d.User1 != null && d.User1.Id == query.UserId) ||
                 (d.User2 != null && d.User2.Id == query.UserId)))
            .OrderByDescending(d => d.StartTime) 
            .Select(d => new DuelHistoryItemDto
            {
                Id = d.Id,
                Status = d.Status,
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                OpponentNickname = d.User1 != null && d.User1.Id == query.UserId
                    ? d.User2!.Nickname
                    : d.User1!.Nickname,
                WinnerNickname = d.Winner != null ? d.Winner.Nickname : null,
                RatingDelta = d.User1 != null && d.User1.Id == query.UserId
                    ? d.User1RatingDelta!.Value
                    : d.User2RatingDelta!.Value
            })
            .ToListAsync(cancellationToken);

        return Result.Ok(duels);
    }
}
