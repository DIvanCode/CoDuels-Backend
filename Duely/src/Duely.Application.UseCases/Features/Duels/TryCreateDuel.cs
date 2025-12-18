using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models;
using System.Text.Json;
using Duely.Application.UseCases.Payloads;
using Microsoft.Extensions.Logging;


namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result>;

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    ITaskiClient taskiClient,
    IOptions<DuelOptions> duelOptions,
    ITaskService taskService,
    Context context,
    ILogger<TryCreateDuelHandler> logger)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pair = duelManager.TryGetPair();

        if (pair is null)
        {
            return Result.Ok();
        }

        logger.LogDebug("Pair selected: {User1} vs {User2}", pair.Value.User1, pair.Value.User2);

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
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
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
        var retryUntil = duel.DeadlineTime.AddMinutes(5);
        var payload1 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User1.Id, MessageType.DuelStarted, duel.Id)
        );

        var payload2 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User2.Id, MessageType.DuelStarted, duel.Id)
        );

        context.Outbox.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = payload1,
            Status = OutboxStatus.ToDo,
            Retries = 0,
            RetryAt = null,
            RetryUntil = retryUntil
        });

        context.Outbox.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = payload2,
            Status = OutboxStatus.ToDo,
            Retries = 0,
            RetryAt = null,
            RetryUntil = retryUntil
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Duel started. DuelId = {DuelId}, Users = {User1}, {User2}, TaskId = {TaskId}, Deadline = {Deadline}",
            duel.Id, duel.User1.Id, duel.User2.Id, duel.TaskId, duel.DeadlineTime
        );

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