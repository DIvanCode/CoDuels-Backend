using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class CheckDuelsForFinishCommand: IRequest { }

public sealed class CheckDuelsForFinishHandler(IMediator mediator, Context context): IRequestHandler<CheckDuelsForFinishCommand>
{
    public async Task Handle(CheckDuelsForFinishCommand request, CancellationToken cancellationToken)
    {  
        var duels = await context.Duels
            .Where(d => d.Status == DuelStatus.InProgress)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .Include(d => d.Submissions
                .Where(s => s.Status == SubmissionStatus.Done && s.Verdict == "Accepted")
                .OrderBy(s => s.SubmitTime))
            .ThenInclude(s => s.User)
            .ToListAsync(cancellationToken);

        foreach (var duel in duels)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var accepted = duel.Submissions;
            var u1 = accepted.FirstOrDefault(s => s.User.Id == duel.User1.Id);
            var u2 = accepted.FirstOrDefault(s => s.User.Id == duel.User2.Id);
            if (u1 is not null || u2 is not null)
            {
                User? winner = null;
                if (u1 is not null && u2 is null)
                {
                    winner = duel.User1;
                }
                if (u2 is not null && u1 is null)
                {
                    winner = duel.User2;
                }
                if (u2 is not null && u1 is not null)
                {
                    if (u1.SubmitTime > u2.SubmitTime)
                    {
                        winner = duel.User2;
                    }
                    else if (u1.SubmitTime < u2.SubmitTime)
                    {
                        winner = duel.User1;
                    }
                }

                var command = new FinishDuelCommand
                {
                    DuelId = duel.Id,
                    WinnerId = winner?.Id
                };
                await mediator.Send(command, cancellationToken);
                continue;
            }

            if (DateTime.UtcNow >= duel.DeadlineTime)
            {
                var command = new FinishDuelCommand
                {
                    DuelId = duel.Id,
                    WinnerId = null
                };
                await mediator.Send(command, cancellationToken);
            }
        }
    }
}