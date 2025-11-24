using Duely.Domain.Models;
using Microsoft.Extensions.Options;

namespace Duely.Domain.Services.Duels;

public interface ITaskService
{
    DuelTask? ChooseTask(User user1, User user2, IReadOnlyCollection<DuelTask> tasks);
}

public sealed class TaskService(IOptions<DuelOptions> options) : ITaskService
{
    public DuelTask? ChooseTask(User user1, User user2, IReadOnlyCollection<DuelTask> tasksList)
    {
        var solvedTasks = user1.Duels.Select(d => d.TaskId)
            .Union(user2.Duels.Select(d => d.TaskId))
            .ToList();
        var tasks = tasksList
            .ExceptBy(solvedTasks, task => task.Id)
            .ToList();
        if (tasks.Count == 0)
        {
            return null;
        }
        
        var avgRating = (user1.Rating + user2.Rating) / 2;
        var bestLevel = options.Value.RatingToTaskLevelMapping
            .Where(item => item.GetInterval().MinRating <= avgRating && avgRating <= item.GetInterval().MaxRating)
            .Select(item => item.Level)
            .SingleOrDefault();
        if (bestLevel == default)
        {
            return tasks[Random.Shared.Next(tasks.Count)];
        }

        var bestTasks = tasks
            .GroupBy(task => task.Level)
            .MinBy(group => Math.Abs(group.Key - bestLevel))!
            .ToList();
        return bestTasks[Random.Shared.Next(bestTasks.Count)];
    }
}