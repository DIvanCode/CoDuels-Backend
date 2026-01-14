using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetIncomingDuelRequestsQuery : IRequest<Result<List<DuelRequestDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetIncomingDuelRequestsHandler(Context context, IDuelManager duelManager)
    : IRequestHandler<GetIncomingDuelRequestsQuery, Result<List<DuelRequestDto>>>
{
    public async Task<Result<List<DuelRequestDto>>> Handle(
        GetIncomingDuelRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var incoming = duelManager
            .GetWaitingUsers()
            .Where(user => user.ExpectedOpponentId == query.UserId)
            .OrderBy(user => user.EnqueuedAt)
            .ToList();

        if (incoming.Count == 0)
        {
            return new List<DuelRequestDto>();
        }

        var incomingUserIds = incoming.Select(user => user.UserId).ToList();
        var nicknames = await context.Users
            .Where(user => incomingUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Nickname })
            .ToDictionaryAsync(user => user.Id, user => user.Nickname, cancellationToken);

        var result = incoming
            .Where(user => nicknames.ContainsKey(user.UserId))
            .Select(user => new DuelRequestDto
            {
                OpponentNickname = nicknames[user.UserId],
                CreatedAt = user.EnqueuedAt
            })
            .ToList();

        return result;
    }
}
