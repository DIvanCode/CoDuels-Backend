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
}
