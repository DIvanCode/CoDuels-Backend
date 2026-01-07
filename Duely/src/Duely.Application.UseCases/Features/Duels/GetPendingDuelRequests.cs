using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetPendingDuelRequestsQuery : IRequest<Result<PendingDuelRequestsDto>>
{
    public required int UserId { get; init; }
}

public sealed class GetPendingDuelRequestsHandler(Context context)
    : IRequestHandler<GetPendingDuelRequestsQuery, Result<PendingDuelRequestsDto>>
{
    public async Task<Result<PendingDuelRequestsDto>> Handle(
        GetPendingDuelRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == query.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }

        var pending = await context.Duels
            .Where(d => d.Status == DuelStatus.Pending &&
                        (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Configuration)
            .ToListAsync(cancellationToken);

        var incoming = pending
            .Where(d => d.User2.Id == query.UserId)
            .OrderBy(d => d.StartTime)
            .Select(d => new DuelRequestDto
            {
                Id = d.Id,
                ConfigurationId = d.Configuration.Id,
                OpponentNickname = d.User1.Nickname,
                CreatedAt = d.StartTime
            })
            .ToList();

        var outgoing = pending
            .Where(d => d.User1.Id == query.UserId)
            .OrderBy(d => d.StartTime)
            .Select(d => new DuelRequestDto
            {
                Id = d.Id,
                ConfigurationId = d.Configuration.Id,
                OpponentNickname = d.User2.Nickname,
                CreatedAt = d.StartTime
            })
            .ToList();

        return new PendingDuelRequestsDto
        {
            Incoming = incoming,
            Outgoing = outgoing
        };
    }
}
