using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Domain.Models.Messages;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Services.Duels;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForFinishCommand: IRequest<Result> { }

public sealed class CheckDuelsForFinishHandler(
    Context context,
    IMessageSender messageSender,
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
            .Include(d => d.Submissions
                .OrderBy(s => s.SubmitTime))
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (duel is null)
        {
            return Result.Ok();
        }

        if (duel.Status == DuelStatus.Finished)
        {
            return Result.Ok();
        }

        var earliestAccepted = duel.Submissions
            .Where(s => s.Status == SubmissionStatus.Done && s.Verdict == "Accepted" && s.SubmitTime <= duel.DeadlineTime)
            .OrderBy(s => s.SubmitTime)
            .FirstOrDefault();


        if (earliestAccepted is not null)
        {
            var evenEarlierNotDone = duel.Submissions
                .Any(s => s.Status != SubmissionStatus.Done && s.SubmitTime <= earliestAccepted.SubmitTime);

            if (evenEarlierNotDone)
            {
                return Result.Ok();
            }

            await FinishDuelAsync(duel, earliestAccepted.User, cancellationToken);

            return Result.Ok();

        }
        ratingManager.UpdateRatings(duel);

        if (duel.DeadlineTime <= DateTime.UtcNow)
        {
            var notDoneBeforeDeadline = duel.Submissions
                .Any(s => s.Status != SubmissionStatus.Done && s.SubmitTime <= duel.DeadlineTime);

            if (notDoneBeforeDeadline)
            {
                return Result.Ok();
            }

            await FinishDuelAsync(duel, null, cancellationToken);

            return Result.Ok();

        }

        return Result.Ok();
    }


    private async Task FinishDuelAsync(Duel duel, User? winner, CancellationToken cancellationToken)
    {
        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        duel.Winner = winner;

        await context.SaveChangesAsync(cancellationToken);

        var message = new DuelFinishedMessage { DuelId = duel.Id };

        await messageSender.SendMessage(duel.User1.Id, message, cancellationToken);
        await messageSender.SendMessage(duel.User2.Id, message, cancellationToken);
    }

}
