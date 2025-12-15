using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class GetDuelHandler(Context context, IRatingManager ratingManager)
    : IRequestHandler<GetDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Id == query.DuelId)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), query.DuelId);
        }

        if (duel.User1.Id != query.UserId && duel.User2.Id != query.UserId)
        {
            return new ForbiddenError(nameof(Duel), "get", nameof(Duel.Id), query.DuelId);  
        }

        var winnerId = duel.Winner?.Id;
        var ratingChanges = new Dictionary<int, Dictionary<DuelResult, int>>
        {
            [duel.User1.Id] = ratingManager.GetRatingChanges(duel, duel.User1InitRating, duel.User2InitRating),
            [duel.User2.Id] = ratingManager.GetRatingChanges(duel, duel.User2InitRating, duel.User1InitRating)
        };
        
        return new DuelDto
        {
            Id = duel.Id,
            TaskId = duel.TaskId,
            Participants = [
                new UserDto
                {
                    Id = duel.User1.Id,
                    Nickname = duel.User1.Nickname,
                    Rating = duel.User1InitRating,
                    CreatedAt = duel.User1.CreatedAt
                },
                new UserDto
                {
                    Id = duel.User2.Id,
                    Nickname = duel.User2.Nickname,
                    Rating = duel.User2InitRating,
                    CreatedAt = duel.User2.CreatedAt
                }
            ],
            WinnerId = winnerId,
            Status = duel.Status,
            StartTime = duel.StartTime,
            DeadlineTime = duel.DeadlineTime,
            EndTime = duel.EndTime,
            RatingChanges = ratingChanges
        };
    }
}