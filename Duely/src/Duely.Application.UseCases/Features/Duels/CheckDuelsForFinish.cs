using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Services.Duels;
using MediatR;
using FluentResults;
using System.Text.Json;
using Duely.Application.Services.Outbox.Payloads;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForFinishCommand : IRequest<Result>;

public sealed class CheckDuelsForFinishHandler(
    Context context,
    IRatingManager ratingManager,
    ITaskService taskService,
    ILogger<CheckDuelsForFinishHandler> logger) : IRequestHandler<CheckDuelsForFinishCommand, Result>
{
    public async Task<Result> Handle(CheckDuelsForFinishCommand request, CancellationToken cancellationToken)
    {
        var duel = await context.Duels
            .Where(d => d.Status == DuelStatus.InProgress &&
            (
                d.DeadlineTime <= DateTime.UtcNow ||
                d.Submissions.Any(
                    s => s.Status == SubmissionStatus.Done && s.Verdict == "Accepted"
                )
            )
            ).OrderBy(d => d.DeadlineTime)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(duel => duel.Winner)
            .Include(d => d.Submissions
                .OrderBy(s => s.SubmitTime))
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(cancellationToken);
        if (duel is null)
        {
            return Result.Ok();
        }

        var taskWinners = taskService.GetSolvedTaskWinners(duel);
        if (AreAllTasksSolved(duel, taskWinners))
        {
            var winner = GetWinnerBySolvedTasks(duel, taskWinners);
            await FinishDuelAsync(duel, winner, cancellationToken);

            logger.LogInformation("Duel finished. DuelId = {DuelId}, WinnerId = {Winner}, Reason = {Reason}",
                duel.Id, duel.Winner?.Id, "AllTasksSolved"
            );

            return Result.Ok();
        }

        if (duel.DeadlineTime <= DateTime.UtcNow)
        {
            var notDoneBeforeDeadline = duel.Submissions.Any(s =>
                s.Status != SubmissionStatus.Done && s.SubmitTime <= duel.DeadlineTime);
            if (notDoneBeforeDeadline)
            {
                return Result.Ok();
            }

            var winner = GetWinnerBySolvedTasks(duel, taskWinners);
            await FinishDuelAsync(duel, winner, cancellationToken);
            
            logger.LogInformation("Duel finished. DuelId = {DuelId}, Reason = {Reason}",
                duel.Id, "Deadline"
            );
            
            return Result.Ok();
        }

        return Result.Ok();
    }

    private async Task FinishDuelAsync(Duel duel, User? winner, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        duel.Winner = winner;
        
        ratingManager.UpdateRatings(duel);
        
        await context.SaveChangesAsync(cancellationToken);

        var retryUntil = duel.DeadlineTime.AddMinutes(5);
        var payload1 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User1.Id, MessageType.DuelFinished, duel.Id)
        );

        var payload2 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User2.Id, MessageType.DuelFinished, duel.Id)
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
    }

    private static bool AreAllTasksSolved(Duel duel, IReadOnlyDictionary<char, int> taskWinners)
        => duel.Tasks.Count > 0 && taskWinners.Count == duel.Tasks.Count;

    private static User? GetWinnerBySolvedTasks(Duel duel, IReadOnlyDictionary<char, int> taskWinners)
    {
        var user1Solved = taskWinners.Values.Count(id => id == duel.User1.Id);
        var user2Solved = taskWinners.Values.Count(id => id == duel.User2.Id);

        if (user1Solved == user2Solved)
        {
            return null;
        }

        return user1Solved > user2Solved ? duel.User1 : duel.User2;
    }
}
