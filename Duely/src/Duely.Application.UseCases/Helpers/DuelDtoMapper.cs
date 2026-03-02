using Duely.Application.UseCases.Dtos;
using Duely.Domain.Models.Duels;
using Duely.Domain.Services.Duels;

namespace Duely.Application.UseCases.Helpers;

public static class DuelDtoMapper
{
    public static DuelDto Map(Duel duel, int viewerId, IRatingManager ratingManager, ITaskService taskService)
    {
        var winner = duel.Winner?.Id;
        var ratingChanges = new Dictionary<int, Dictionary<DuelResult, int>>
        {
            [duel.User1.Id] = ratingManager.GetRatingChanges(duel, duel.User1InitRating, duel.User2InitRating),
            [duel.User2.Id] = ratingManager.GetRatingChanges(duel, duel.User2InitRating, duel.User1InitRating)
        };

        var isParticipant = duel.User1.Id == viewerId || duel.User2.Id == viewerId;
        var visibleTasks = isParticipant
            ? taskService.GetVisibleTasks(duel, viewerId)
            : new Dictionary<char, DuelTask>();

        var tasks = MapTasks(duel.Tasks, visibleTasks);
        var solutions = new Dictionary<char, DuelTaskSolutionDto>();
        Dictionary<char, DuelTaskSolutionDto>? opponentSolutions = null;
        if (isParticipant)
        {
            solutions = MapSolutions(
                duel.User1.Id == viewerId ? duel.User1Solutions : duel.User2Solutions,
                visibleTasks.Keys);
            if (duel.Configuration.ShouldShowOpponentSolution)
            {
                opponentSolutions = MapSolutions(
                    duel.User1.Id == viewerId ? duel.User2Solutions : duel.User1Solutions,
                    visibleTasks.Keys);
            }
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
            WinnerId = winner,
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
