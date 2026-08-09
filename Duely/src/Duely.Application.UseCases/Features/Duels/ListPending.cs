using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels.Pending;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetPendingDuelsQuery : IRequest<Result<List<PendingDuelDto>>>;

public sealed class GetPendingDuelsHandler(Context context)
    : IRequestHandler<GetPendingDuelsQuery, Result<List<PendingDuelDto>>>
{
    public async Task<Result<List<PendingDuelDto>>> Handle(
        GetPendingDuelsQuery query,
        CancellationToken cancellationToken)
    {
        var friendlyDuels = await context.PendingDuels
            .AsNoTracking()
            .OfType<FriendlyPendingDuel>()
            .Select(duel => new PendingDuelDto
            {
                Id = duel.Id,
                Type = duel.Type,
                CreatedAt = duel.CreatedAt,
                User1 = new UserDto
                {
                    Id = duel.User1.Id,
                    Nickname = duel.User1.Nickname,
                    Rating = duel.User1.Rating,
                    CreatedAt = duel.User1.CreatedAt
                },
                User2 = new UserDto
                {
                    Id = duel.User2.Id,
                    Nickname = duel.User2.Nickname,
                    Rating = duel.User2.Rating,
                    CreatedAt = duel.User2.CreatedAt
                },
                IsAcceptedByUser1 = true,
                IsAcceptedByUser2 = duel.IsAccepted
            })
            .ToListAsync(cancellationToken);

        var groupDuels = await context.PendingDuels
            .AsNoTracking()
            .OfType<GroupPendingDuel>()
            .Select(duel => new PendingDuelDto
            {
                Id = duel.Id,
                Type = duel.Type,
                CreatedAt = duel.CreatedAt,
                User1 = new UserDto
                {
                    Id = duel.User1.Id,
                    Nickname = duel.User1.Nickname,
                    Rating = duel.User1.Rating,
                    CreatedAt = duel.User1.CreatedAt
                },
                User2 = new UserDto
                {
                    Id = duel.User2.Id,
                    Nickname = duel.User2.Nickname,
                    Rating = duel.User2.Rating,
                    CreatedAt = duel.User2.CreatedAt
                },
                IsAcceptedByUser1 = duel.IsAcceptedByUser1,
                IsAcceptedByUser2 = duel.IsAcceptedByUser2,
                GroupId = duel.Group.Id,
                GroupName = duel.Group.Name
            })
            .ToListAsync(cancellationToken);

        var tournamentDuels = await context.PendingDuels
            .AsNoTracking()
            .OfType<TournamentPendingDuel>()
            .Select(duel => new PendingDuelDto
            {
                Id = duel.Id,
                Type = duel.Type,
                CreatedAt = duel.CreatedAt,
                User1 = new UserDto
                {
                    Id = duel.User1.Id,
                    Nickname = duel.User1.Nickname,
                    Rating = duel.User1.Rating,
                    CreatedAt = duel.User1.CreatedAt
                },
                User2 = new UserDto
                {
                    Id = duel.User2.Id,
                    Nickname = duel.User2.Nickname,
                    Rating = duel.User2.Rating,
                    CreatedAt = duel.User2.CreatedAt
                },
                IsAcceptedByUser1 = duel.IsAcceptedByUser1,
                IsAcceptedByUser2 = duel.IsAcceptedByUser2,
                GroupId = duel.Tournament.Group.Id,
                GroupName = duel.Tournament.Group.Name,
                TournamentId = duel.Tournament.Id,
                TournamentName = duel.Tournament.Name
            })
            .ToListAsync(cancellationToken);

        return friendlyDuels
            .Concat(groupDuels)
            .Concat(tournamentDuels)
            .OrderByDescending(duel => duel.CreatedAt)
            .ToList();
    }
}

public sealed class GetRankedDuelSearchersQuery : IRequest<Result<List<RankedDuelSearcherDto>>>;

public sealed class GetRankedDuelSearchersHandler(Context context)
    : IRequestHandler<GetRankedDuelSearchersQuery, Result<List<RankedDuelSearcherDto>>>
{
    public async Task<Result<List<RankedDuelSearcherDto>>> Handle(
        GetRankedDuelSearchersQuery query,
        CancellationToken cancellationToken)
    {
        return await context.PendingDuels
            .AsNoTracking()
            .OfType<RankedPendingDuel>()
            .OrderBy(duel => duel.CreatedAt)
            .Select(duel => new RankedDuelSearcherDto
            {
                User = new UserDto
                {
                    Id = duel.User.Id,
                    Nickname = duel.User.Nickname,
                    Rating = duel.User.Rating,
                    CreatedAt = duel.User.CreatedAt
                },
                Rating = duel.Rating,
                SearchStartedAt = duel.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
