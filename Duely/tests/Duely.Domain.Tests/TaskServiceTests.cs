using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class TaskServiceTests
{
    [Fact]
    public void ChooseTask_NoTasksProvided_ReturnsNull()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        
        var tasksService = new TaskService();
        var task = tasksService.ChooseTask(user1, user2, 1, new List<DuelTask>().AsReadOnly());
        
        task.Should().BeNull();
    }
    
    [Fact]
    public void ChooseTask_AllTasksSolved_ReturnsNull()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        CreateDuel(1, user1, user2, "task-1");
        var tasks = new List<DuelTask> { new("task-1", 1, []) };
        
        var tasksService = new TaskService();
        var task = tasksService.ChooseTask(user1, user2, 1, tasks.AsReadOnly());
        
        task.Should().BeNull();
    }
    
    [Fact]
    public void ChooseTask_OneTaskUnsolved_ReturnsUnsolvedTask()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        CreateDuel(1, user1, user2, "task-1");
        var tasks = new List<DuelTask>
        {
            new("task-1", 1, []),
            new("task-2", 1, [])
        };
        
        var tasksService = new TaskService();
        var task = tasksService.ChooseTask(user1, user2, 1, tasks.AsReadOnly());

        task.Should().NotBeNull();
        task!.Id.Should().Be("task-2");
        task.Level.Should().Be(1);
    }
    
    [Fact]
    public void ChooseTask_ChooseByTaskLevel_ReturnsBestTask()
    {
        var user1 = CreateUser(1, 700);
        var user2 = CreateUser(2, 700);
        var tasks = new List<DuelTask>
        {
            new("task-1", 1, []),
            new("task-2", 2, []),
            new("task-3", 3, [])
        };
        
        var tasksService = new TaskService();
        var task = tasksService.ChooseTask(user1, user2, 2, tasks.AsReadOnly());

        task.Should().NotBeNull();
        task!.Id.Should().Be("task-2");
        task.Level.Should().Be(2);
    }
    
    [Fact]
    public void ChooseTask_ChooseByTaskLevel_BestTaskSolved_ReturnsClosestToBestTask()
    {
        var user1 = CreateUser(1, 400);
        var user2 = CreateUser(2, 400);
        CreateDuel(1, user1, user2, "task-1");
        var tasks = new List<DuelTask>
        {
            new("task-1", 1, []),
            new("task-2", 2, []),
            new("task-3", 3, [])
        };
        
        var tasksService = new TaskService();
        var task = tasksService.ChooseTask(user1, user2, 1, tasks.AsReadOnly());

        task.Should().NotBeNull();
        task!.Id.Should().Be("task-2");
        task.Level.Should().Be(2);
    }

    [Fact]
    public void TryChooseTasks_SelectsBestMatchByTopicsAndLevel()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);

        var configuration = new DuelConfiguration
        {
            Id = 1,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 2,
                    Topics = ["dp", "graphs"]
                }
            }
        };

        var tasks = new List<DuelTask>
        {
            new("task-1", 1, ["dp"]),
            new("task-2", 5, ["dp", "graphs", "math"]),
            new("task-3", 2, ["dp", "graphs"])
        };

        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeTrue();
        chosen.Should().ContainKey('A');
        chosen['A'].Id.Should().Be("task-3");
    }

    [Fact]
    public void GetSolvedTaskWinners_EarliestAcceptedWins()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var now = DateTime.UtcNow;

        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, []),
            ['B'] = new("task-2", 1, [])
        });

        duel.Submissions.Add(MakeSubmission(1, duel, user1, 'A', now.AddSeconds(1), SubmissionStatus.Done, "Accepted"));
        duel.Submissions.Add(MakeSubmission(2, duel, user2, 'A', now.AddSeconds(2), SubmissionStatus.Done, "Accepted"));
        duel.Submissions.Add(MakeSubmission(3, duel, user2, 'B', now.AddSeconds(3), SubmissionStatus.Done, "Accepted"));

        var tasksService = new TaskService();

        var winners = tasksService.GetSolvedTaskWinners(duel);

        winners.Should().HaveCount(2);
        winners['A'].Should().Be(user1.Id);
        winners['B'].Should().Be(user2.Id);
    }

    [Fact]
    public void GetVisibleTasks_Sequential_ReturnsSolvedTasksAndFirstUnsolved()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var now = DateTime.UtcNow;

        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, []),
            ['B'] = new("task-2", 1, []),
            ['C'] = new("task-3", 1, [])
        });

        duel.Submissions.Add(MakeSubmission(1, duel, user1, 'A', now.AddSeconds(1), SubmissionStatus.Done, "Accepted"));
        duel.Submissions.Add(MakeSubmission(2, duel, user2, 'B', now.AddSeconds(2), SubmissionStatus.Done, "Accepted"));

        var tasksService = new TaskService();

        var visible = tasksService.GetVisibleTasks(duel, user1.Id);

        visible.Should().ContainKey('A');
        visible.Should().ContainKey('B');
        visible.Should().ContainKey('C');
    }

    [Fact]
    public void TryChooseTasks_AssignsBestTaskToBestPosition()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);

        var configuration = new DuelConfiguration
        {
            Id = 2,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = ["dp"]
                },
                ['B'] = new()
                {
                    Level = 3,
                    Topics = ["graphs"]
                }
            }
        };

        var tasks = new List<DuelTask>
        {
            new("task-1", 3, ["graphs"]),
            new("task-2", 1, ["dp", "math"])
        };

        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeTrue();
        chosen['A'].Id.Should().Be("task-2");
        chosen['B'].Id.Should().Be("task-1");
    }

    [Fact]
    public void TryChooseTasks_PicksBestPartialMatchWhenFullMatchMissing()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);

        var configuration = new DuelConfiguration
        {
            Id = 3,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 3,
                    Topics = ["dp", "graphs"]
                }
            }
        };

        var tasks = new List<DuelTask>
        {
            new("task-1", 3, ["dp"]),
            new("task-2", 5, ["graphs"])
        };

        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeTrue();
        chosen['A'].Id.Should().Be("task-1");
    }

    [Fact]
    public void GetVisibleTasks_Sequential_ReturnsAllTasksWhenAllSolved()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var now = DateTime.UtcNow;

        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, []),
            ['B'] = new("task-2", 1, [])
        });

        duel.Submissions.Add(MakeSubmission(1, duel, user1, 'A', now.AddSeconds(1), SubmissionStatus.Done, "Accepted"));
        duel.Submissions.Add(MakeSubmission(2, duel, user2, 'B', now.AddSeconds(2), SubmissionStatus.Done, "Accepted"));

        var tasksService = new TaskService();

        var visible = tasksService.GetVisibleTasks(duel, user1.Id);

        visible.Should().ContainKey('A');
        visible.Should().ContainKey('B');
    }

    [Fact]
    public void GetVisibleTasks_Parallel_ReturnsAllTasks()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);

        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, []),
            ['B'] = new("task-2", 1, [])
        }, DuelTasksOrder.Parallel);

        var tasksService = new TaskService();

        var visible = tasksService.GetVisibleTasks(duel, user1.Id);

        visible.Should().ContainKey('A');
        visible.Should().ContainKey('B');
    }

    [Fact]
    public void TryChooseTasks_ReturnsFalse_WhenTasksEmpty()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var configuration = new DuelConfiguration
        {
            Id = 10,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        };

        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, [], out var chosen);

        success.Should().BeFalse();
        chosen.Should().BeEmpty();
    }

    [Fact]
    public void TryChooseTasks_ReturnsFalse_WhenConfigurationsEmpty()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var configuration = new DuelConfiguration
        {
            Id = 11,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 0,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>()
        };

        var tasks = new List<DuelTask> { new("task-1", 1, []) };
        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeFalse();
        chosen.Should().BeEmpty();
    }

    [Fact]
    public void TryChooseTasks_ReturnsFalse_WhenNotEnoughTasks()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var configuration = new DuelConfiguration
        {
            Id = 12,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 2,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] },
                ['B'] = new() { Level = 1, Topics = [] }
            }
        };

        var tasks = new List<DuelTask> { new("task-1", 1, []) };
        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeFalse();
        chosen.Should().ContainKey('A');
        chosen.Should().NotContainKey('B');
    }

    [Fact]
    public void TryChooseTasks_FallsBackToSolvedTasks_WhenAllSolved()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        CreateDuel(1, user1, user2, "task-1");

        var configuration = new DuelConfiguration
        {
            Id = 13,
            Owner = user1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                ['A'] = new() { Level = 1, Topics = [] }
            }
        };

        var tasks = new List<DuelTask> { new("task-1", 1, []) };
        var tasksService = new TaskService();
        var success = tasksService.TryChooseTasks(user1, user2, configuration, tasks, out var chosen);

        success.Should().BeTrue();
        chosen.Should().ContainKey('A');
        chosen['A'].Id.Should().Be("task-1");
    }

    [Fact]
    public void GetSolvedTaskWinners_ReturnsEmpty_WhenNoSubmissions()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, [])
        });

        var tasksService = new TaskService();
        var winners = tasksService.GetSolvedTaskWinners(duel);

        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetSolvedTaskWinners_IgnoresAcceptedAfterDeadline()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var now = DateTime.UtcNow;
        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, [])
        });
        duel.DeadlineTime = now.AddSeconds(5);
        duel.Submissions.Add(MakeSubmission(1, duel, user1, 'A', now.AddSeconds(10), SubmissionStatus.Done, "Accepted"));

        var tasksService = new TaskService();
        var winners = tasksService.GetSolvedTaskWinners(duel);

        winners.Should().BeEmpty();
    }

    [Fact]
    public void GetSolvedTaskWinners_IgnoresAcceptedWhenEarlierNotDoneExists()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var now = DateTime.UtcNow;

        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, [])
        });

        duel.Submissions.Add(MakeSubmission(1, duel, user1, 'A', now.AddSeconds(1), SubmissionStatus.Queued, null));
        duel.Submissions.Add(MakeSubmission(2, duel, user2, 'A', now.AddSeconds(2), SubmissionStatus.Done, "Accepted"));

        var tasksService = new TaskService();
        var winners = tasksService.GetSolvedTaskWinners(duel);

        winners.Should().BeEmpty();
    }

    [Fact]
    public void IsTaskVisible_ReturnsFalse_ForHiddenSequentialTask()
    {
        var user1 = CreateUser(1, 0);
        var user2 = CreateUser(2, 0);
        var duel = CreateDuelWithTasks(1, user1, user2, new Dictionary<char, DuelTask>
        {
            ['A'] = new("task-1", 1, []),
            ['B'] = new("task-2", 1, [])
        });

        var tasksService = new TaskService();
        tasksService.IsTaskVisible(duel, user1.Id, 'B').Should().BeFalse();
    }
    
    private static User CreateUser(int id, int rating)
    {
        if (id != 1 && id != 2)
        {
            throw new ArgumentException("Invalid id");
        }
        
        var user = new User
        {
            Id = id,
            Nickname = $"user{id}",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        };

        return user;
    }
    
    private static Duel CreateDuel(int id, User u1, User u2, string task)
    {
        const char taskKey = 'A';
        var configuration = new DuelConfiguration
        {
            Id = id,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                [taskKey] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        };
        
        var duel = new Duel
        {
            Id = id,
            Configuration = configuration,
            Status = DuelStatus.Finished,
            Tasks = new Dictionary<char, DuelTask>
            {
                [taskKey] = new(task, 1, [])
            },
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            EndTime = DateTime.UtcNow,
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };
        u1.DuelsAsUser1.Add(duel);
        u2.DuelsAsUser2.Add(duel);
        return duel;
    }

    private static Duel CreateDuelWithTasks(
        int id,
        User u1,
        User u2,
        Dictionary<char, DuelTask> tasks,
        DuelTasksOrder tasksOrder = DuelTasksOrder.Sequential)
    {
        var configuration = new DuelConfiguration
        {
            Id = id,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = tasks.Count,
            TasksOrder = tasksOrder,
            TasksConfigurations = tasks.ToDictionary(
                t => t.Key,
                t => new DuelTaskConfiguration
                {
                    Level = t.Value.Level,
                    Topics = t.Value.Topics
                })
        };

        return new Duel
        {
            Id = id,
            Configuration = configuration,
            Status = DuelStatus.InProgress,
            Tasks = tasks,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            User1 = u1,
            User1InitRating = 1500,
            User2 = u2,
            User2InitRating = 1500
        };
    }

    private static Submission MakeSubmission(
        int id,
        Duel duel,
        User user,
        char taskKey,
        DateTime submitTime,
        SubmissionStatus status,
        string? verdict)
    {
        return new Submission
        {
            Id = id,
            Duel = duel,
            User = user,
            TaskKey = taskKey,
            Code = "code",
            Language = "lang",
            SubmitTime = submitTime,
            Status = status,
            Verdict = verdict
        };
    }
}
