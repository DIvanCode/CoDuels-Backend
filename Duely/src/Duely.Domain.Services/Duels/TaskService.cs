using Duely.Domain.Models;
namespace Duely.Domain.Services.Duels;

public interface ITaskService
{
    DuelTask? ChooseTask(User user1, User user2, int taskLevel, IReadOnlyCollection<DuelTask> tasks);
    bool TryChooseTasks(
        User user1,
        User user2,
        DuelConfiguration configuration,
        IReadOnlyCollection<DuelTask> tasks,
        out Dictionary<char, DuelTask> chosenTasks);
    IReadOnlyDictionary<char, DuelTask> GetVisibleTasks(Duel duel, int userId);
    bool IsTaskVisible(Duel duel, int userId, char taskKey);
    IReadOnlyDictionary<char, int> GetSolvedTaskWinners(Duel duel);
}

public sealed class TaskService : ITaskService
{
    public DuelTask? ChooseTask(
        User user1,
        User user2,
        int taskLevel,
        IReadOnlyCollection<DuelTask> tasksList)
    {
        var solvedTasks = GetSolvedTaskIds(user1, user2);
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

    public bool TryChooseTasks(
        User user1,
        User user2,
        DuelConfiguration configuration,
        IReadOnlyCollection<DuelTask> tasks,
        out Dictionary<char, DuelTask> chosenTasks)
    {
        chosenTasks = new Dictionary<char, DuelTask>();
        if (tasks.Count == 0 || configuration.TasksConfigurations.Count == 0)
        {
            return false;
        }

        var solvedTasks = GetSolvedTaskIds(user1, user2);
        var availableTasks = tasks.ExceptBy(solvedTasks, task => task.Id).ToList();
        if (availableTasks.Count == 0)
        {
            availableTasks = tasks.ToList();
        }

        var remainingConfigurations = configuration.TasksConfigurations
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        while (remainingConfigurations.Count > 0)
        {
            var bestPair = SelectBestTaskAssignment(availableTasks, remainingConfigurations);
            if (bestPair is null)
            {
                return false;
            }

            chosenTasks[bestPair.Value.TaskKey] = bestPair.Value.Task;
            availableTasks.Remove(bestPair.Value.Task);
            remainingConfigurations.Remove(bestPair.Value.TaskKey);

            if (availableTasks.Count == 0 && remainingConfigurations.Count > 0)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyDictionary<char, DuelTask> GetVisibleTasks(Duel duel, int userId)
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

    public bool IsTaskVisible(Duel duel, int userId, char taskKey)
    {
        var visibleTasks = GetVisibleTasks(duel, userId);
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

    private static HashSet<string> GetSolvedTaskIds(User user1, User user2)
    {
        return user1.Duels.SelectMany(d => d.Tasks.Values.Select(t => t.Id))
            .Union(user2.Duels.SelectMany(d => d.Tasks.Values.Select(t => t.Id)))
            .ToHashSet();
    }

    private static DuelTask? SelectBestTask(
        IReadOnlyCollection<DuelTask> tasks,
        DuelTaskConfiguration configuration)
    {
        if (tasks.Count == 0)
        {
            return null;
        }

        var requiredTopics = configuration.Topics ?? [];
        return tasks
            .Select(task => new
            {
                Task = task,
                TopicMatches = CountTopicMatches(task.Topics, requiredTopics),
                LevelDiff = Math.Abs(task.Level - configuration.Level),
                FullMatch = requiredTopics.Length > 0 && ContainsAllTopics(task.Topics, requiredTopics)
            })
            .OrderByDescending(x => x.FullMatch)
            .ThenByDescending(x => x.TopicMatches)
            .ThenBy(x => x.LevelDiff)
            .ThenBy(_ => Random.Shared.Next())
            .First()
            .Task;
    }

    private static int CountTopicMatches(string[] taskTopics, string[] requiredTopics)
    {
        if (requiredTopics.Length == 0 || taskTopics.Length == 0)
        {
            return 0;
        }

        var taskTopicsSet = new HashSet<string>(taskTopics);
        var count = 0;
        foreach (var topic in requiredTopics)
        {
            if (taskTopicsSet.Contains(topic))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ContainsAllTopics(string[] taskTopics, string[] requiredTopics)
    {
        if (requiredTopics.Length == 0)
        {
            return true;
        }

        if (taskTopics.Length == 0)
        {
            return false;
        }

        var taskTopicsSet = new HashSet<string>(taskTopics);
        foreach (var topic in requiredTopics)
        {
            if (!taskTopicsSet.Contains(topic))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct TaskAssignment(char TaskKey, DuelTask Task);

    private static TaskAssignment? SelectBestTaskAssignment(
        IReadOnlyCollection<DuelTask> tasks,
        IReadOnlyDictionary<char, DuelTaskConfiguration> configurations)
    {
        TaskAssignment? bestAssignment = null;
        var bestScore = (FullMatch: false, TopicMatches: -1, LevelDiff: int.MaxValue);

        foreach (var task in tasks)
        {
            foreach (var configEntry in configurations)
            {
                var requiredTopics = configEntry.Value.Topics ?? [];
                var topicMatches = CountTopicMatches(task.Topics, requiredTopics);
                var levelDiff = Math.Abs(task.Level - configEntry.Value.Level);
                var fullMatch = ContainsAllTopics(task.Topics, requiredTopics);

                var candidateScore = (fullMatch, topicMatches, levelDiff);
                if (IsBetterScore(candidateScore, bestScore))
                {
                    bestScore = candidateScore;
                    bestAssignment = new TaskAssignment(configEntry.Key, task);
                }
            }
        }

        return bestAssignment;
    }

    private static bool IsBetterScore(
        (bool FullMatch, int TopicMatches, int LevelDiff) candidate,
        (bool FullMatch, int TopicMatches, int LevelDiff) current)
    {
        if (candidate.FullMatch != current.FullMatch)
        {
            return candidate.FullMatch;
        }

        if (candidate.TopicMatches != current.TopicMatches)
        {
            return candidate.TopicMatches > current.TopicMatches;
        }

        return candidate.LevelDiff < current.LevelDiff;
    }
}
