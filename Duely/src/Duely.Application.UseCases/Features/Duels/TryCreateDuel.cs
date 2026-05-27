using Duely.Application.Services.Errors;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Domain.Services.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Microsoft.Extensions.Logging;
using Duely.Domain.Services.Tournaments;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result>;

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    IOptions<DuelOptions> duelOptions,
    ITaskiClient taskiClient,
    ITaskService taskService,
    ITournamentMatchmakingStrategyResolver tournamentMatchmakingStrategyResolver,
    Context context,
    ILogger<TryCreateDuelHandler> logger)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pendingDuels = new List<PendingDuel>();
        
        pendingDuels.AddRange(await context.PendingDuels.OfType<RankedPendingDuel>()
            .Include(d => d.User)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User)
            .ThenInclude(u => u.DuelsAsUser2)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.Configuration)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<GroupPendingDuel>()
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.Configuration)
            .Include(d => d.Group)
            .Include(d => d.CreatedBy)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<TournamentPendingDuel>()
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User1)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser1)
            .Include(d => d.User2)
            .ThenInclude(u => u.DuelsAsUser2)
            .Include(d => d.Configuration)
            .Include(d => d.Tournament)
            .ToListAsync(cancellationToken));

        var pairs = duelManager.GetPairs(pendingDuels);
        foreach (var pair in pairs)
        {
            var configuration = pair.Configuration ?? new DuelConfiguration
            {
                Owner = null,
                IsRated = pair.IsRated,
                ShouldShowOpponentSolution = true,
                MaxDurationMinutes = duelOptions.Value.DefaultMaxDurationMinutes,
                TasksCount = 1,
                TasksOrder = DuelTasksOrder.Sequential
            };
            
            var tasksResult = await ChooseTasksAsync(
                pair.User1,
                pair.User2,
                configuration,
                cancellationToken);
            if (tasksResult.IsFailed)
            {
                return tasksResult.ToResult();
            }

            var startTime = DateTime.UtcNow;
            var deadlineTime = startTime.AddMinutes(configuration.MaxDurationMinutes);

            var duel = new Duel
            {
                Status = DuelStatus.InProgress,
                Configuration = configuration,
                Tasks = tasksResult.Value,
                User1Solutions = [],
                User2Solutions = [],
                StartTime = startTime,
                DeadlineTime = deadlineTime,
                User1 = pair.User1,
                User1InitRating = pair.User1.Rating,
                User2 = pair.User2,
                User2InitRating = pair.User2.Rating,
            };

            context.PendingDuels.RemoveRange(pair.UsedPendingDuels);
            context.Duels.Add(duel);
            await context.SaveChangesAsync(cancellationToken);

            var groupPendingDuel = pair.UsedPendingDuels.OfType<GroupPendingDuel>().SingleOrDefault();
            if (groupPendingDuel is not null)
            {
                context.Add(new GroupDuel
                {
                    Group = groupPendingDuel.Group,
                    Duel = duel,
                    CreatedBy = groupPendingDuel.CreatedBy
                });
            }

            var tournamentPendingDuel = pair.UsedPendingDuels.OfType<TournamentPendingDuel>().SingleOrDefault();
            if (tournamentPendingDuel is not null)
            {
                var strategy = tournamentMatchmakingStrategyResolver
                    .GetStrategy(tournamentPendingDuel.Tournament.MatchmakingType);
                strategy.AttachDuel(tournamentPendingDuel.Tournament, tournamentPendingDuel, duel);
            }

            var retryUntil = duel.DeadlineTime.AddMinutes(5);

            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = duel.User1.Id,
                    Message = new DuelStartedMessage
                    {
                        DuelId = duel.Id
                    }
                },
                RetryUntil = retryUntil
            });

            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.SendMessage,
                Payload = new SendMessagePayload
                {
                    UserId = duel.User2.Id,
                    Message = new DuelStartedMessage
                    {
                        DuelId = duel.Id
                    }
                },
                RetryUntil = retryUntil
            });
            
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Duel started. DuelId = {DuelId}, Users = {User1}, {User2}, Deadline = {Deadline}",
                duel.Id, duel.User1.Id, duel.User2.Id, duel.DeadlineTime);
        }
        
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task<Result<Dictionary<char, DuelTask>>> ChooseTasksAsync(
        User user1,
        User user2,
        DuelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var tasksResult = await taskiClient.GetTasksListAsync(cancellationToken);
        if (tasksResult.IsFailed)
        {
            return tasksResult.ToResult();
        }

        var solvedTasks = await context.Duels
            .AsNoTracking()
            .Where(duel => duel.User1.Id == user1.Id || duel.User2.Id == user2.Id ||
                           duel.User2.Id == user1.Id || duel.User1.Id == user2.Id)
            .SelectMany(duel => duel.Tasks.Select(x => x.Value.Id))
            .ToHashSetAsync(cancellationToken);

        var tasks = tasksResult.Value.Tasks
            .Select(x => (Task: new DuelTask(x.Id), WasSolved: solvedTasks.Contains(x.Id)))
            .Select(x => (x.Task, x.WasSolved))
            .ToList();
        var chosenTasks = taskService.ChooseTasks(tasks, configuration.TasksCount);
        if (chosenTasks.Count < configuration.TasksCount)
        {
            return new EntityNotFoundError("Not enough tasks present in the system");
        }

        return chosenTasks;
    }
}
