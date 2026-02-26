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
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result>;

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    ITaskiClient taskiClient,
    IOptions<DuelOptions> duelOptions,
    IRatingManager ratingManager,
    ITaskService taskService,
    Context context,
    ILogger<TryCreateDuelHandler> logger)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    private const char DefaultTaskKey = 'A';
    
    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pendingDuels = new List<PendingDuel>();
        
        pendingDuels.AddRange(await context.PendingDuels
            .OfType<RankedPendingDuel>()
            .Include(p => p.User)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels
            .OfType<FriendlyPendingDuel>()
            .Include(p => p.User1)
            .Include(p => p.User2)
            .Include(p => p.Configuration)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels
            .OfType<GroupPendingDuel>()
            .Include(p => p.User1)
            .Include(p => p.User2)
            .Include(p => p.Configuration)
            .ToListAsync(cancellationToken));

        var pairs = duelManager.GetPairs(pendingDuels).ToList();
        if (pairs.Count == 0)
        {
            return Result.Ok();
        }

        foreach (var pair in pairs)
        {
            DuelConfiguration configuration;
            if (pair.Configuration is null)
            {
                configuration = new DuelConfiguration
                {
                    Owner = null,
                    IsRated = pair.IsRated,
                    ShouldShowOpponentSolution = true,
                    MaxDurationMinutes = duelOptions.Value.DefaultMaxDurationMinutes,
                    TasksCount = 1,
                    TasksOrder = DuelTasksOrder.Sequential,
                    TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
                    {
                        [DefaultTaskKey] = new()
                        {
                            Level = ratingManager.GetTaskLevel((pair.User1.Rating + pair.User2.Rating) / 2),
                            Topics = []
                        }
                    }
                };
            }
            else
            {
                configuration = pair.Configuration;
            }

            var tasksResult = await ChooseTasksAsync(pair.User1, pair.User2, configuration, cancellationToken);
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

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            context.PendingDuels.RemoveRange(pair.UsedPendingDuels);
            context.Duels.Add(duel);
            
            await context.SaveChangesAsync(cancellationToken);

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

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Duel started. DuelId = {DuelId}, Users = {User1}, {User2}, Deadline = {Deadline}",
                duel.Id, duel.User1.Id, duel.User2.Id, duel.DeadlineTime);
        }

        return Result.Ok();
    }

    private async Task<Result<Dictionary<char, DuelTask>>> ChooseTasksAsync(
        User user1,
        User user2,
        DuelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var tasksList = await taskiClient.GetTasksListAsync(cancellationToken);
        if (tasksList.IsFailed)
        {
            return tasksList.ToResult();
        }

        var tasks = tasksList.Value.Tasks.Select(t => new DuelTask(t.Id, t.Level, t.Topics)).ToList();
        if (!taskService.TryChooseTasks(user1, user2, configuration, tasks, out var chosenTasks))
        {
            return new EntityNotFoundError(nameof(DuelTask), "configuration", configuration.Id);
        }

        return chosenTasks;
    }
}
