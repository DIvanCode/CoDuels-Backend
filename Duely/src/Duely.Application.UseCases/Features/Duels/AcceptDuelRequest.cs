using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Services.Duels;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class AcceptDuelRequestCommand : IRequest<Result>
{
    public required int UserId { get; init; }
    public required int DuelId { get; init; }
}

public sealed class AcceptDuelRequestHandler(
    Context context,
    ITaskiClient taskiClient,
    ITaskService taskService)
    : IRequestHandler<AcceptDuelRequestCommand, Result>
{
    public async Task<Result> Handle(AcceptDuelRequestCommand command, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Include(d => d.Configuration)
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser2)
            .SingleOrDefaultAsync(d => d.Id == command.DuelId, cancellationToken);
        if (duel is null)
        {
            return new EntityNotFoundError(nameof(Duel), nameof(Duel.Id), command.DuelId);
        }

        if (duel.Status != DuelStatus.Pending)
        {
            return new ForbiddenError(nameof(Duel), "accept", nameof(Duel.Id), command.DuelId);
        }

        if (duel.User2.Id != command.UserId)
        {
            return new ForbiddenError(nameof(Duel), "accept", nameof(Duel.Id), command.DuelId);
        }

        var tasksList = await taskiClient.GetTasksListAsync(cancellationToken);
        if (tasksList.IsFailed)
        {
            return tasksList.ToResult();
        }

        var tasks = tasksList.Value.Tasks.Select(t => new DuelTask(t.Id, t.Level, t.Topics)).ToList();
        if (!taskService.TryChooseTasks(duel.User1, duel.User2, duel.Configuration, tasks, out var chosenTasks))
        {
            return new EntityNotFoundError(nameof(DuelTask), "configuration", duel.Configuration.Id);
        }

        duel.Status = DuelStatus.InProgress;
        duel.Tasks = chosenTasks;
        duel.StartTime = DateTime.UtcNow;
        duel.DeadlineTime = duel.StartTime.AddMinutes(duel.Configuration.MaxDurationMinutes);
        duel.User1InitRating = duel.User1.Rating;
        duel.User2InitRating = duel.User2.Rating;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
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

        return Result.Ok();
    }
}
