using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models;
using Duely.Application.UseCases.Errors;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Domain.Models.Messages;
using Microsoft.EntityFrameworkCore;
using MediatR;
using FluentResults;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForFinishCommand: IRequest<Result> { }

public sealed class CheckDuelsForFinishHandler(
    Context context,
    IMessageSender messageSender
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
                .Where(s => s.Status == SubmissionStatus.Done && s.Verdict == "Accepted")
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
        
        var winner = getWinner(duel);

        duel.Status = DuelStatus.Finished;
        duel.EndTime = DateTime.UtcNow;
        
        if (winner is not null)
        {
            duel.Winner = winner;
        }

        await context.SaveChangesAsync(cancellationToken);

        var message = new DuelFinishedMessage
        {
            DuelId = duel.Id,
        };

        await messageSender.SendMessage(duel.User1.Id, message, cancellationToken);
        await messageSender.SendMessage(duel.User2.Id, message, cancellationToken);
        
        return Result.Ok();
    }

    private static User? getWinner(Duel duel)
    {
        var accepted = duel.Submissions;
        var u1 = accepted.FirstOrDefault(s => s.User.Id == duel.User1.Id);
        var u2 = accepted.FirstOrDefault(s => s.User.Id == duel.User2.Id);

        if (u1 is null && u2 is null)
        {
            return null;
        }
        if (u1 is not null && u2 is null)
        {
            return duel.User1;
        }
        if (u2 is not null && u1 is null)
        {
            return duel.User2;
        }


        if (u1!.SubmitTime > u2!.SubmitTime)
        {
            return duel.User2;
        }
        if (u1.SubmitTime < u2.SubmitTime)
        {
            return duel.User1;
        }

        return null;
    }
}