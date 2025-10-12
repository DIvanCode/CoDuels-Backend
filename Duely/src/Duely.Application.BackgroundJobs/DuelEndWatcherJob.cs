using Duely.Application.Configuration;
using Duely.Application.UseCases.FinishDuel;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
namespace Duely.Application.BackgroundJobs;

public sealed class DuelEndWatcherJob : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly DuelSettings _settings;

    public DuelEndWatcherJob(IServiceProvider sp, IOptions<DuelSettings> options)
    {
        _sp = sp;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var scope = _sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var now = DateTime.UtcNow;
                var duels = await db.Duels
                    .AsNoTracking()
                    .Where(d => d.Status == DuelStatus.InProgress)
                    .OrderBy(d => d.StartTime)
                    .Take(_settings.FinishBatchSize)
                    .ToListAsync(cancellationToken);

                foreach (var duel in duels)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    var deadline = duel.StartTime.AddMinutes(duel.MaxDuration);
                    if (duel.Result == DuelResult.User1)
                    {
                        await mediator.Send(new FinishDuelCommand { DuelId = duel.Id, Winner = "1" }, cancellationToken);
                        continue;
                    }
                    if (duel.Result == DuelResult.User2)
                    {
                        await mediator.Send(new FinishDuelCommand { DuelId = duel.Id, Winner = "2" }, cancellationToken);
                        continue;
                    }
                    var duelIds = duels.Select(d => d.Id).ToList();
                    var accepted = await db.Submissions
                        .AsNoTracking()
                        .Where(s => duelIds.Contains(s.Duel.Id) && s.Status == SubmissionStatus.Done && s.Verdict == "Accepted")
                        .OrderBy(s => s.SubmitTime)
                        .ToListAsync(cancellationToken);
                    var u1 = accepted.FirstOrDefault(s => s.UserId == duel.User1Id);
                    var u2 = accepted.FirstOrDefault(s => s.UserId == duel.User2Id);
                    if (u1 is not null || u2 is not null)
                    {
                        string winner = "draw";
                        if (u1 is not null && u2 is null)
                        {
                            winner = "1";
                        }
                        if (u2 is not null && u1 is null)
                        {
                            winner = "2";
                        }
                        else if (u2 is not null && u1 is not null)
                        {
                            if (u1.SubmitTime > u2.SubmitTime)
                            {
                                winner = "2";
                            }
                            else if (u1.SubmitTime < u2.SubmitTime)
                            {
                                winner = "1";
                            }
                            else
                            {
                                winner = "draw";
                            }
                        }
                        await mediator.Send(new FinishDuelCommand { DuelId = duel.Id, Winner = winner }, cancellationToken);
                        continue;
                    }
                    if (now >= deadline)
                    {
                        await mediator.Send(new FinishDuelCommand { DuelId = duel.Id, Winner = "draw" }, cancellationToken);
                    }
                }
            }
            await Task.Delay(_settings.CheckFinishInterval, cancellationToken);
        }
    }
}
