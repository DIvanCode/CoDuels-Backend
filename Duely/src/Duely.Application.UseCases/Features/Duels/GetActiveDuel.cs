using Duely.Application.Services.Errors;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class GetActiveDuelQuery : IRequest<Result<DuelDto>>
{
    public required int UserId { get; init; }
}

public sealed class GetActiveDuelHandler(Context context, IRatingManager ratingManager, ITaskService taskService)
    : IRequestHandler<GetActiveDuelQuery, Result<DuelDto>>
{
    public async Task<Result<DuelDto>> Handle(GetActiveDuelQuery query, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Status == DuelStatus.InProgress && 
                (d.User1.Id == query.UserId || d.User2.Id == query.UserId))
            .Include(d => d.Configuration)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .Include(d => d.Submissions)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError($"Active duel for user {query.UserId} not found");
        }

        var winnerId = duel.Winner?.Id;
        var ratingChanges = new Dictionary<int, Dictionary<DuelResult, int>>
        {
            [duel.User1.Id] = ratingManager.GetRatingChanges(duel, duel.User1InitRating, duel.User2InitRating),
            [duel.User2.Id] = ratingManager.GetRatingChanges(duel, duel.User2InitRating, duel.User1InitRating)
        };

        var visibleTasks = taskService.GetVisibleTasks(duel, query.UserId);
        var tasks = MapTasks(duel.Tasks, visibleTasks);
        var solutions = MapSolutions(
            duel.User1.Id == query.UserId ? duel.User1Solutions : duel.User2Solutions,
            visibleTasks.Keys);
        Dictionary<char, DuelTaskSolutionDto>? opponentSolutions = null;
        if (duel.Configuration.ShouldShowOpponentSolution)
        {
            opponentSolutions = MapSolutions(
                duel.User1.Id == query.UserId ? duel.User2Solutions : duel.User1Solutions,
                visibleTasks.Keys);
        }
        
        return new DuelDto
        {
            Id = duel.Id,
            IsRated = duel.Configuration.IsRated,
            ShouldShowOpponentSolution = duel.Configuration.ShouldShowOpponentSolution,
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
            Tasks = tasks,
            Solutions = solutions,
            OpponentSolutions = opponentSolutions
        };
    }

    private static Dictionary<char, DuelTaskDto> MapTasks(
        IReadOnlyDictionary<char, DuelTask> tasks,
        IReadOnlyDictionary<char, DuelTask> visibleTasks)
    {
        return tasks
            .OrderBy(kv => kv.Key)
            .ToDictionary(
                kv => kv.Key,
                kv => new DuelTaskDto
                {
                    Id = visibleTasks.ContainsKey(kv.Key) ? kv.Value.Id : null
                });
    }

    private static Dictionary<char, DuelTaskSolutionDto> MapSolutions(
        Dictionary<char, DuelTaskSolution> solutions,
        IEnumerable<char> taskKeys)
    {
        var result = new Dictionary<char, DuelTaskSolutionDto>();
        foreach (var key in taskKeys)
        {
            if (solutions.TryGetValue(key, out var solution))
            {
                result[key] = new DuelTaskSolutionDto
                {
                    Solution = solution.Solution,
                    Language = solution.Language
                };
            }
        }

        return result;
    }
}
