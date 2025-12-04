using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result>;

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    ITaskiClient taskiClient,
    IMessageSender messageSender,
    IOptions<DuelOptions> duelOptions,
    ITaskService taskService,
    Context context)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pair = duelManager.TryGetPair();

        if (pair is null)
        {
            return Result.Ok();
        }

        var user1 = await context.Users
            .Include(u => u.DuelsAsUser1)
            .Include(u => u.DuelsAsUser2)
            .SingleOrDefaultAsync(u => u.Id == pair.Value.User1, cancellationToken);
        if (user1 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), pair.Value.User1);
        }

        var user2 = await context.Users
            .Include(u => u.DuelsAsUser1)
            .Include(u => u.DuelsAsUser2)
            .SingleOrDefaultAsync(u => u.Id == pair.Value.User2, cancellationToken);
        if (user2 is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), pair.Value.User2);
        }

        var taskResult = await ChooseTaskAsync(user1, user2, cancellationToken);
        if (taskResult.IsFailed)
        {
            return taskResult.ToResult();
        }

        var taskId = taskResult.Value;
        var startTime = DateTime.UtcNow;
        var deadlineTime = startTime.AddMinutes(duelOptions.Value.MaxDurationMinutes);

        var duel = new Duel
        {
            TaskId = taskId,
            Status = DuelStatus.InProgress,
            StartTime = startTime,
            DeadlineTime = deadlineTime,
            User1 = user1,
            User1InitRating = user1.Rating,
            User2 = user2,
            User2InitRating = user2.Rating,
        };

        context.Duels.Add(duel);
        await context.SaveChangesAsync(cancellationToken);

        var message = new DuelStartedMessage
        {
            DuelId = duel.Id,
        };

        await messageSender.SendMessage(duel.User1.Id, message, cancellationToken);
        await messageSender.SendMessage(duel.User2.Id, message, cancellationToken);
        
        Console.WriteLine($"Started duel {duel.Id}");

        return Result.Ok();
    }

    private async Task<Result<string>> ChooseTaskAsync(User user1, User user2, CancellationToken cancellationToken)
    {
        var tasksList = await taskiClient.GetTasksListAsync(cancellationToken);
        if (tasksList.IsFailed)
        {
            return tasksList.ToResult();
        }

        var chosenTask = taskService.ChooseTask(user1, user2,
            tasksList.Value.Tasks.Select(t => new DuelTask(t.Id, t.Level)).ToList());
        if (chosenTask is not null)
        {
            return chosenTask.Id;
        }
        
        var randomTaskResult = await taskiClient.GetRandomTaskIdAsync(cancellationToken);
        if (randomTaskResult.IsFailed)
        {
            return randomTaskResult.ToResult();
        }

        return randomTaskResult.Value;
    }
}