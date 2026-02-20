using System;
using Duely.Domain.Models;

namespace Duely.Application.Tests.TestHelpers;

public static class EntityFactory
{
    public static User MakeUser(int id, string nickname = "user")
    {
        var salt = Guid.NewGuid().ToString("N");
        var hash = BCrypt.Net.BCrypt.HashPassword("pwd" + salt);
        return new User
        {
            Id = id,
            Nickname = nickname,
            PasswordSalt = salt,
            PasswordHash = hash,
            Rating = 1500,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Group MakeGroup(int id, string name = "group")
    {
        return new Group
        {
            Id = id,
            Name = name
        };
    }

    public static UserGroupRole MakeUserGroupRole(User user, Group group, GroupRole role)
    {
        return new UserGroupRole
        {
            User = user,
            Group = group,
            Role = role
        };
    }

    public static Duel MakeDuel(int id, User u1, User u2, string taskId = "TASK-1",
                                DateTime? start = null, DateTime? deadline = null)
    {
        start ??= DateTime.UtcNow;
        deadline ??= start.Value.AddMinutes(30);
        const char taskKey = 'A';

        var configuration = new DuelConfiguration
        {
            Id = id,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentSolution = false,
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
        
        return new Duel
        {
            Id = id,
            Configuration = configuration,
            Status = DuelStatus.InProgress,
            Tasks = new Dictionary<char, DuelTask>
            {
                [taskKey] = new(taskId, 1, [])
            },
            User1Solutions = new Dictionary<char, DuelTaskSolution>
            {
                [taskKey] = new DuelTaskSolution
                {
                    Solution = string.Empty,
                    Language = Language.Python
                }
            },
            User2Solutions = new Dictionary<char, DuelTaskSolution>
            {
                [taskKey] = new DuelTaskSolution
                {
                    Solution = string.Empty,
                    Language = Language.Python
                }
            },
            StartTime = start.Value,
            DeadlineTime = deadline.Value,
            User1 = u1,
            User2 = u2,
            User1InitRating = 1500,
            User2InitRating = 1500
        };
    }

    public static Submission MakeSubmission(int id, Duel duel, User user,
        string solution = "print(1)", Language language = Language.Python,
        DateTime? time = null, SubmissionStatus status = SubmissionStatus.Queued,
        string? verdict = null, string? message = null, char taskKey = 'A')
    {
        return new Submission
        {
            Id = id,
            Duel = duel,
            User = user,
            TaskKey = taskKey,
            Solution = solution,
            Language = language,
            SubmitTime = time ?? DateTime.UtcNow,
            Status = status,
            Verdict = verdict,
            Message = message,
            IsUpsolving = false
        };
    }
}
