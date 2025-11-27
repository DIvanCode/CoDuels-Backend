using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetCurrentDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
}

public sealed class GetCurrentDuelHandler(Context context, IRatingManager ratingManager)
    : IRequestHandler<GetCurrentDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetCurrentDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Status == DuelStatus.InProgress && 
                (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError($"Current duel for user {query.UserId} not found");
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
                    Rating = duel.User1InitRating
                },
                new UserDto
                {
                    Id = duel.User2.Id,
                    Nickname = duel.User2.Nickname,
                    Rating = duel.User2InitRating
                }
            ],
            WinnerId = winnerId,
            Status = duel.Status,
            StartTime = duel.StartTime,
            DeadlineTime = duel.DeadlineTime,
            RatingChanges = ratingChanges
        };
    }
}