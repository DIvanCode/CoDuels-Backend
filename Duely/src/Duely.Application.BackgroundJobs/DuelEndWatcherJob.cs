using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Duely.Application.UseCases.Features.Duels;

namespace Duely.Application.BackgroundJobs;

public sealed class DuelEndWatcherJob(IServiceProvider sp, IOptions<DuelEndWatcherJobOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = sp.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Context>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

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

            await Task.Delay(options.Value.CheckIntervalMs, cancellationToken);
        }
    }
}
