using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Services.Duels;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duely.Application.UseCases.Features.Duels;

public sealed class TryCreateDuelCommand : IRequest<Result>;

public sealed class TryCreateDuelHandler(
    IDuelManager duelManager,
    ITaskiClient taskiClient,
    IOptions<DuelOptions> duelOptions,
    IRatingManager ratingManager,
    ITaskService taskService,
    ITournamentMatchmakingStrategyResolver tournamentMatchmakingStrategyResolver,
    Context context,
    ILogger<TryCreateDuelHandler> logger)
    : IRequestHandler<TryCreateDuelCommand, Result>
{
    private const char DefaultTaskKey = 'A';

    public async Task<Result> Handle(TryCreateDuelCommand request, CancellationToken cancellationToken)
    {
        var pendingDuels = await LoadPendingDuelSnapshotAsync(cancellationToken);
        var pairs = duelManager.GetPairs(pendingDuels).ToList();
        if (pairs.Count == 0)
        {
            return Result.Ok();
        }

        // The catalog is deliberately fresh for one matchmaking tick and is never reused across ticks.
        var tasksList = await taskiClient.GetTasksListAsync(cancellationToken);
        if (tasksList.IsFailed)
        {
            logger.LogWarning(
                "Duel matchmaking tick stopped because the Taski catalog could not be loaded. Reasons = {Reasons}",
                FormatErrors(tasksList.Errors));
            return tasksList.ToResult();
        }

        var taskCatalog = tasksList.Value.Tasks
            .Select(task => new DuelTask(task.Id, task.Level, task.Topics))
            .ToList();
        var reservedTaskIds = new HashSet<string>();
        var pairErrors = new List<IError>();

        foreach (var candidate in pairs)
        {
            context.ChangeTracker.Clear();

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                var userIds = new[] { candidate.User1.Id, candidate.User2.Id }
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();
                var users = await context.Users
                    .Where(user => userIds.Contains(user.Id))
                    .OrderBy(user => user.Id)
                    .ForUpdate()
                    .ToListAsync(cancellationToken);
                if (users.Count != userIds.Length)
                {
                    logger.LogWarning(
                        "Duel pair was skipped because a user disappeared. User1Id = {User1Id}, User2Id = {User2Id}",
                        candidate.User1.Id,
                        candidate.User2.Id);
                    await RollbackAndClearAsync(transaction, cancellationToken);
                    continue;
                }

                var pendingIds = candidate.UsedPendingDuels
                    .Select(pending => pending.Id)
                    .OrderBy(id => id)
                    .ToArray();
                var lockedPendingIds = await context.PendingDuels
                    .Where(pending => pendingIds.Contains(pending.Id))
                    .OrderBy(pending => pending.Id)
                    .ForUpdate()
                    .Select(pending => pending.Id)
                    .ToListAsync(cancellationToken);
                if (!pendingIds.SequenceEqual(lockedPendingIds))
                {
                    logger.LogInformation(
                        "Duel pair was skipped because its pending state was already consumed or canceled. User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}",
                        candidate.User1.Id,
                        candidate.User2.Id,
                        pendingIds);
                    await RollbackAndClearAsync(transaction, cancellationToken);
                    continue;
                }

                var currentPendingDuels = await LoadPendingDuelsAsync(
                    candidate.UsedPendingDuels[0].Type,
                    pendingIds,
                    cancellationToken);
                var pair = duelManager.GetPairs(currentPendingDuels)
                    .SingleOrDefault(current => HasSamePendingRows(candidate, current));
                if (pair is null)
                {
                    logger.LogInformation(
                        "Duel pair was skipped because it is no longer ready. User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}",
                        candidate.User1.Id,
                        candidate.User2.Id,
                        pendingIds);
                    await RollbackAndClearAsync(transaction, cancellationToken);
                    continue;
                }

                var hasActiveDuel = await context.Duels
                    .AsNoTracking()
                    .AnyAsync(duel =>
                            duel.Status == DuelStatus.InProgress &&
                            (userIds.Contains(EF.Property<int>(duel, "User1Id")) ||
                             userIds.Contains(EF.Property<int>(duel, "User2Id"))),
                        cancellationToken);
                if (hasActiveDuel)
                {
                    logger.LogInformation(
                        "Duel pair was skipped because at least one user already has an active duel. User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}",
                        pair.User1.Id,
                        pair.User2.Id,
                        pendingIds);
                    await RollbackAndClearAsync(transaction, cancellationToken);
                    continue;
                }

                var configuration = pair.Configuration ?? CreateDefaultConfiguration(pair);
                var previouslyUsedTaskIds = await LoadPreviouslyUsedTaskIdsAsync(userIds, cancellationToken);
                var tasksResult = ChooseTasks(
                    configuration,
                    previouslyUsedTaskIds,
                    taskCatalog,
                    reservedTaskIds);
                if (tasksResult.IsFailed)
                {
                    logger.LogWarning(
                        "Duel pair was not created because tasks could not be selected. User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}, ConfigurationId = {ConfigurationId}, Reasons = {Reasons}",
                        pair.User1.Id,
                        pair.User2.Id,
                        pendingIds,
                        configuration.Id,
                        FormatErrors(tasksResult.Errors));
                    pairErrors.Add(new Error(
                        $"Could not select tasks for users {pair.User1.Id} and {pair.User2.Id}."));
                    await RollbackAndClearAsync(transaction, cancellationToken);
                    continue;
                }

                var startTime = DateTime.UtcNow;
                var duel = new Duel
                {
                    Status = DuelStatus.InProgress,
                    Configuration = configuration,
                    Tasks = tasksResult.Value,
                    User1Solutions = [],
                    User2Solutions = [],
                    StartTime = startTime,
                    DeadlineTime = startTime.AddMinutes(configuration.MaxDurationMinutes),
                    User1 = pair.User1,
                    User1InitRating = pair.User1.Rating,
                    User2 = pair.User2,
                    User2InitRating = pair.User2.Rating,
                };

                context.PendingDuels.RemoveRange(pair.UsedPendingDuels);
                context.Duels.Add(duel);
                await context.SaveChangesAsync(cancellationToken);

                AttachDuel(pair, duel);
                AddDuelStartedMessages(duel);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                reservedTaskIds.UnionWith(duel.Tasks.Values.Select(task => task.Id));
                logger.LogInformation(
                    "Duel started. DuelId = {DuelId}, User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}, Deadline = {Deadline}",
                    duel.Id,
                    duel.User1.Id,
                    duel.User2.Id,
                    pendingIds,
                    duel.DeadlineTime);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                context.ChangeTracker.Clear();
                throw;
            }
            catch (Exception exception)
            {
                context.ChangeTracker.Clear();
                pairErrors.Add(new Error(
                    $"Could not create a duel for users {candidate.User1.Id} and {candidate.User2.Id}: {exception.Message}"));
                logger.LogError(
                    exception,
                    "Duel pair failed without stopping the matchmaking tick. User1Id = {User1Id}, User2Id = {User2Id}, PendingIds = {PendingIds}",
                    candidate.User1.Id,
                    candidate.User2.Id,
                    candidate.UsedPendingDuels.Select(pending => pending.Id).ToArray());
            }
        }

        return pairErrors.Count == 0
            ? Result.Ok()
            : Result.Fail(pairErrors);
    }

    private async Task<List<PendingDuel>> LoadPendingDuelSnapshotAsync(CancellationToken cancellationToken)
    {
        var pendingDuels = new List<PendingDuel>();

        pendingDuels.AddRange(await context.PendingDuels.OfType<RankedPendingDuel>()
            .AsNoTracking()
            .Include(pending => pending.User)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .Include(pending => pending.User1)
            .Include(pending => pending.User2)
            .Include(pending => pending.Configuration)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<GroupPendingDuel>()
            .AsNoTracking()
            .Include(pending => pending.User1)
            .Include(pending => pending.User2)
            .Include(pending => pending.Configuration)
            .Include(pending => pending.Group)
            .Include(pending => pending.CreatedBy)
            .ToListAsync(cancellationToken));
        pendingDuels.AddRange(await context.PendingDuels.OfType<TournamentPendingDuel>()
            .AsNoTracking()
            .Include(pending => pending.User1)
            .Include(pending => pending.User2)
            .Include(pending => pending.Configuration)
            .Include(pending => pending.Tournament)
            .ToListAsync(cancellationToken));

        return pendingDuels;
    }

    private async Task<List<PendingDuel>> LoadPendingDuelsAsync(
        PendingDuelType type,
        int[] pendingIds,
        CancellationToken cancellationToken)
    {
        return type switch
        {
            PendingDuelType.Ranked => (await context.PendingDuels.OfType<RankedPendingDuel>()
                    .Where(pending => pendingIds.Contains(pending.Id))
                    .Include(pending => pending.User)
                    .ToListAsync(cancellationToken))
                .Cast<PendingDuel>()
                .ToList(),
            PendingDuelType.Friendly => (await context.PendingDuels.OfType<FriendlyPendingDuel>()
                    .Where(pending => pendingIds.Contains(pending.Id))
                    .Include(pending => pending.User1)
                    .Include(pending => pending.User2)
                    .Include(pending => pending.Configuration)
                    .ToListAsync(cancellationToken))
                .Cast<PendingDuel>()
                .ToList(),
            PendingDuelType.Group => (await context.PendingDuels.OfType<GroupPendingDuel>()
                    .Where(pending => pendingIds.Contains(pending.Id))
                    .Include(pending => pending.User1)
                    .Include(pending => pending.User2)
                    .Include(pending => pending.Configuration)
                    .Include(pending => pending.Group)
                    .Include(pending => pending.CreatedBy)
                    .ToListAsync(cancellationToken))
                .Cast<PendingDuel>()
                .ToList(),
            PendingDuelType.Tournament => (await context.PendingDuels.OfType<TournamentPendingDuel>()
                    .Where(pending => pendingIds.Contains(pending.Id))
                    .Include(pending => pending.User1)
                    .Include(pending => pending.User2)
                    .Include(pending => pending.Configuration)
                    .Include(pending => pending.Tournament)
                    .ToListAsync(cancellationToken))
                .Cast<PendingDuel>()
                .ToList(),
            _ => []
        };
    }

    private DuelConfiguration CreateDefaultConfiguration(DuelPair pair)
    {
        return new DuelConfiguration
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

    private async Task<HashSet<string>> LoadPreviouslyUsedTaskIdsAsync(
        int[] userIds,
        CancellationToken cancellationToken)
    {
        var taskMaps = await context.Duels
            .AsNoTracking()
            .Where(duel =>
                userIds.Contains(EF.Property<int>(duel, "User1Id")) ||
                userIds.Contains(EF.Property<int>(duel, "User2Id")))
            .Select(duel => duel.Tasks)
            .ToListAsync(cancellationToken);

        return taskMaps
            .SelectMany(tasks => tasks.Values)
            .Select(task => task.Id)
            .ToHashSet();
    }

    private Result<Dictionary<char, DuelTask>> ChooseTasks(
        DuelConfiguration configuration,
        IReadOnlySet<string> previouslyUsedTaskIds,
        IReadOnlyCollection<DuelTask> taskCatalog,
        IReadOnlySet<string> reservedTaskIds)
    {
        var tasksForSelection = taskCatalog
            .ExceptBy(reservedTaskIds, task => task.Id)
            .ToList();
        if (tasksForSelection.Count < configuration.TasksConfigurations.Count)
        {
            tasksForSelection = taskCatalog.ToList();
        }

        if (!taskService.TryChooseTasks(
                configuration,
                tasksForSelection,
                previouslyUsedTaskIds,
                out var chosenTasks))
        {
            return new EntityNotFoundError(nameof(DuelTask), "configuration", configuration.Id);
        }

        return chosenTasks;
    }

    private void AttachDuel(DuelPair pair, Duel duel)
    {
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
    }

    private void AddDuelStartedMessages(Duel duel)
    {
        var retryUntil = duel.DeadlineTime.AddMinutes(5);
        context.OutboxMessages.Add(CreateDuelStartedMessage(duel.User1.Id, duel.Id, retryUntil));
        context.OutboxMessages.Add(CreateDuelStartedMessage(duel.User2.Id, duel.Id, retryUntil));
    }

    private static OutboxMessage CreateDuelStartedMessage(int userId, int duelId, DateTime retryUntil)
    {
        return new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = userId,
                Message = new DuelStartedMessage
                {
                    DuelId = duelId
                }
            },
            RetryUntil = retryUntil
        };
    }

    private async Task RollbackAndClearAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    private static bool HasSamePendingRows(DuelPair expected, DuelPair current)
    {
        return expected.UsedPendingDuels
            .Select(pending => pending.Id)
            .ToHashSet()
            .SetEquals(current.UsedPendingDuels.Select(pending => pending.Id));
    }

    private static string FormatErrors(IEnumerable<IError> errors)
    {
        return string.Join("; ", errors.Select(error => error.Message));
    }
}
