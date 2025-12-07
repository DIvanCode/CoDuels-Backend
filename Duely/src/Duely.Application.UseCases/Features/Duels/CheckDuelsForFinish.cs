using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Domain.Models.Messages;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Services.Duels;
using MediatR;
using FluentResults;
using System.Text.Json;
using Duely.Application.UseCases.Payloads;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForFinishCommand : IRequest<Result>;

public sealed class CheckDuelsForFinishHandler(
    Context context,
    IRatingManager ratingManager
    ) 
    : IRequestHandler<CheckDuelsForFinishCommand, Result>
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

        var earliestAccepted = duel.Submissions
            .Where(s =>
                s.SubmitTime <= duel.DeadlineTime &&
                s.Status == SubmissionStatus.Done &&
                s.Verdict == "Accepted")
            .OrderBy(s => s.SubmitTime)
            .FirstOrDefault();

        if (earliestAccepted is not null)
        {
            var evenEarlierNotDone = duel.Submissions.Any(s =>
                s.Status != SubmissionStatus.Done && s.SubmitTime <= earliestAccepted.SubmitTime);
            if (evenEarlierNotDone)
            {
                return Result.Ok();
            }

            await FinishDuelAsync(duel, earliestAccepted.User, cancellationToken);
            Console.WriteLine($"Finished duel {duel.Id}");

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

            await FinishDuelAsync(duel, null, cancellationToken);
            Console.WriteLine($"Finished duel {duel.Id}");
            
            return Result.Ok();
        }

        return Result.Ok();
    }

    private async Task FinishDuelAsync(Duel duel, User? winner, CancellationToken cancellationToken)
    {
        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        duel.Winner = winner;
        
        ratingManager.UpdateRatings(duel);
        
        await context.SaveChangesAsync(cancellationToken);

        var retryUntil = duel.DeadlineTime.AddMinutes(5);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var payload1 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User1.Id, MessageType.DuelFinished, duel.Id),
            jsonOptions
        );

        var payload2 = JsonSerializer.Serialize(
            new SendMessagePayload(duel.User2.Id, MessageType.DuelFinished, duel.Id),
            jsonOptions
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
    }
}
