using Duely.Domain.Models;
using Duely.Domain.Models.Duels;

namespace Duely.Domain.Services.Duels;

public interface ITaskService
{
    Dictionary<char, DuelTask> ChooseTasks(
        IReadOnlyCollection<(DuelTask Task, bool WasSolved)> tasks,
        int tasksCount);
    IReadOnlyDictionary<char, DuelTask> GetVisibleTasks(Duel duel);
    bool IsTaskVisible(Duel duel, char taskKey);
    IReadOnlyDictionary<char, int> GetSolvedTaskWinners(Duel duel);
}

public sealed class TaskService : ITaskService
{
    public Dictionary<char, DuelTask> ChooseTasks(
        IReadOnlyCollection<(DuelTask Task, bool WasSolved)> tasks,
        int tasksCount)
    {
        var tasksToChoose = tasks
            .Where(x => !x.WasSolved)
            .Select(x => x.Task)
            .ToList();
        if (tasksToChoose.Count < tasksCount)
        {
            var solvedTasks = tasks
                .Where(x => x.WasSolved)
                .Select(x => x.Task)
                .ToArray();
            tasksToChoose.AddRange(ChooseRandomTasks(solvedTasks, tasksCount - tasksToChoose.Count));
        }
        
        var chosenTasksList = ChooseRandomTasks(tasksToChoose.ToArray(), tasksCount);
        var chosenTasks = new Dictionary<char, DuelTask>();
        for (var i = 0; i < chosenTasksList.Count; i++)
        {
            chosenTasks[(char)('A' + i)] = chosenTasksList[i]; 
        }

        return chosenTasks;
    }

    public IReadOnlyDictionary<char, DuelTask> GetVisibleTasks(Duel duel)
    {
        if (duel.Configuration.TasksOrder == DuelTasksOrder.Parallel)
        {
            return new Dictionary<char, DuelTask>(duel.Tasks);
        }

        var winners = GetSolvedTaskWinners(duel);
        var visibleKeys = new HashSet<char>(winners.Keys);

        var orderedKeys = duel.Tasks.Keys.OrderBy(k => k).ToList();
        var hasUnsolved = orderedKeys.Any(k => !winners.ContainsKey(k));
        if (hasUnsolved)
        {
            var firstUnsolved = orderedKeys.First(k => !winners.ContainsKey(k));
            visibleKeys.Add(firstUnsolved);
        }

        return duel.Tasks
            .Where(kv => visibleKeys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public bool IsTaskVisible(Duel duel, char taskKey)
    {
        var visibleTasks = GetVisibleTasks(duel);
        return visibleTasks.ContainsKey(taskKey);
    }

    public IReadOnlyDictionary<char, int> GetSolvedTaskWinners(Duel duel)
    {
        if (duel.Submissions.Count == 0)
        {
            return new Dictionary<char, int>();
        }

        var winners = new Dictionary<char, int>();
        var orderedKeys = duel.Tasks.Keys.OrderBy(k => k).ToList();

        foreach (var taskKey in orderedKeys)
        {
            var submissions = duel.Submissions
                .Where(s => s.TaskKey == taskKey && s.SubmitTime <= duel.DeadlineTime)
                .OrderBy(s => s.SubmitTime)
                .ThenBy(s => s.Id)
                .ToList();

            var earliestAccepted = submissions
                .FirstOrDefault(s => s.Status == SubmissionStatus.Done && s.Verdict == "Accepted");
            if (earliestAccepted is null)
            {
                continue;
            }

            var hasEarlierNotDone = submissions.Any(s =>
                s.Status != SubmissionStatus.Done && s.SubmitTime <= earliestAccepted.SubmitTime);
            if (hasEarlierNotDone)
            {
                continue;
            }

            winners[taskKey] = earliestAccepted.User.Id;
        }

        return winners;
    }

    private static List<DuelTask> ChooseRandomTasks(DuelTask[] tasks, int count)
    {
        Random.Shared.Shuffle(tasks);
        return tasks.Take(count).ToList();
    }
}
