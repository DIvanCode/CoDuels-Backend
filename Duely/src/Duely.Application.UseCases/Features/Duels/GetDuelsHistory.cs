using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetDuelsHistoryQuery : IRequest<Result<List<DuelDto>>>
{
    public required int UserId { get; init; }
}

public sealed class GetDuelsHistoryHandler(Context context, IRatingManager ratingManager)
    : IRequestHandler<GetDuelsHistoryQuery, Result<List<DuelDto>>>
{
    public async Task<Result<List<DuelDto>>> Handle(GetDuelsHistoryQuery query, CancellationToken cancellationToken)
    {
        var user = await context.Users.SingleOrDefaultAsync(
            u => u.Id == query.UserId,
            cancellationToken);
        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), query.UserId);
        }
        
        var duels = await context.Duels
            .Where(d => d.Status == DuelStatus.Finished &&
                        (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(duel => duel.Configuration)
            .Include(duel => duel.User1)
            .Include(duel => duel.User2)
            .Include(duel => duel.Winner)
            .OrderByDescending(d => d.StartTime)
            .ToListAsync(cancellationToken);

        return duels
            .Select(duel =>
            {
                var winnerId = duel.Winner?.Id;
                var ratingChanges = new Dictionary<int, Dictionary<DuelResult, int>>
                {
                    [duel.User1.Id] = ratingManager.GetRatingChanges(duel, duel.User1InitRating, duel.User2InitRating),
                    [duel.User2.Id] = ratingManager.GetRatingChanges(duel, duel.User2InitRating, duel.User1InitRating)
                };
        
                return new DuelDto
                {
                    Id = duel.Id,
                    IsRated = duel.Configuration.IsRated,
                    ShouldShowOpponentCode = duel.Configuration.ShouldShowOpponentCode,
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
                    RatingChanges = ratingChanges,
                    TasksOrder = duel.Configuration.TasksOrder,
                    // TODO: Return only visible tasks
                    Tasks = duel.Tasks.ToDictionary(
                        task => task.Key,
                        task => new DuelTaskDto
                        {
                            Id = task.Value.Id
                        })
                };
            })
            .ToList();
    }
}
