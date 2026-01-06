using Duely.Domain.Models;
using Microsoft.Extensions.Options;

namespace Duely.Domain.Services.Duels;

public interface ITaskService
{
    DuelTask? ChooseTask(User user1, User user2, int taskLevel, IReadOnlyCollection<DuelTask> tasks);
}

public sealed class TaskService : ITaskService
{
    public DuelTask? ChooseTask(
        User user1,
        User user2,
        int taskLevel,
        IReadOnlyCollection<DuelTask> tasksList)
    {
        var solvedTasks = user1.Duels.SelectMany(d => d.Tasks.Values.Select(t => t.Id))
            .Union(user2.Duels.SelectMany(d => d.Tasks.Values.Select(t => t.Id)))
            .ToList();
        var tasks = tasksList
            .ExceptBy(solvedTasks, task => task.Id)
            .ToList();
        if (tasks.Count == 0)
        {
            return null;
        }

        var bestTasks = tasks
            .GroupBy(task => task.Level)
            .MinBy(group => Math.Abs(group.Key - taskLevel))!
            .ToList();
        return bestTasks[Random.Shared.Next(bestTasks.Count)];
    }
}