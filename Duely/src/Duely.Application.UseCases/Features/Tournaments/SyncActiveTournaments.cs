using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Services.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duely.Application.UseCases.Features.Tournaments;

public sealed class SyncActiveTournamentsCommand : IRequest<Result>;

public sealed class SyncActiveTournamentsHandler(
    Context context,
    ITournamentMatchmakingStrategyResolver strategyResolver,
    ILogger<SyncActiveTournamentsHandler> logger)
    : IRequestHandler<SyncActiveTournamentsCommand, Result>
{
    public async Task<Result> Handle(SyncActiveTournamentsCommand request, CancellationToken cancellationToken)
    {
        var tournaments = await context.Tournaments
            .Where(t => t.Status == TournamentStatus.InProgress)
            .Include(t => t.DuelConfiguration)
            .Include(t => t.Participants)
            .ThenInclude(p => p.User)
            .ToListAsync(cancellationToken);

        if (tournaments.Count == 0)
        {
            return Result.Ok();
        }

        var activeDuelUserIds = await context.Duels
            .AsNoTracking()
            .Where(d => d.Status == Duely.Domain.Models.Duels.DuelStatus.InProgress)
            .Select(d => d.User1.Id)
            .Concat(context.Duels
                .AsNoTracking()
                .Where(d => d.Status == Duely.Domain.Models.Duels.DuelStatus.InProgress)
                .Select(d => d.User2.Id))
            .Distinct()
            .ToListAsync(cancellationToken);
        var busyUserIds = activeDuelUserIds.ToHashSet();

        var pendingUsers = await GetPendingUserIdsAsync(cancellationToken);
        busyUserIds.UnionWith(pendingUsers);

        var duelIds = tournaments
            .SelectMany(tournament => strategyResolver
                .GetStrategy(tournament.MatchmakingType)
                .GetReferencedDuelIds(tournament))
            .Distinct()
            .ToList();
        var duelsById = await context.Duels
            .Include(d => d.Winner)
            .Where(d => duelIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var existingPendingDuels = await context.PendingDuels
            .OfType<TournamentPendingDuel>()
            .Include(d => d.Tournament)
            .Include(d => d.User1)
            .Include(d => d.User2)
            .ToListAsync(cancellationToken);

        foreach (var tournament in tournaments)
        {
            var strategy = strategyResolver.GetStrategy(tournament.MatchmakingType);
            strategy.Sync(tournament, duelsById);
            var participantsById = tournament.Participants.ToDictionary(p => p.User.Id, p => p.User);
            var tournamentPendingDuels = existingPendingDuels
                .Where(d => d.Tournament.Id == tournament.Id)
                .ToList();

            foreach (var candidate in strategy.GetPendingDuelCandidates(tournament, duelsById))
            {
                if (tournamentPendingDuels.Any(d => strategy.HasPendingDuel(tournament, d, candidate)))
                {
                    continue;
                }

                if (!participantsById.TryGetValue(candidate.User1Id, out var user1) ||
                    !participantsById.TryGetValue(candidate.User2Id, out var user2))
                {
                    continue;
                }

                if (busyUserIds.Contains(user1.Id) || busyUserIds.Contains(user2.Id))
                {
                    continue;
                }

                context.PendingDuels.Add(new TournamentPendingDuel
                {
                    Type = PendingDuelType.Tournament,
                    Tournament = tournament,
                    User1 = user1,
                    User2 = user2,
                    Configuration = tournament.DuelConfiguration,
                    CreatedAt = DateTime.UtcNow
                });

                context.OutboxMessages.AddRange(
                    new OutboxMessage
                    {
                        Type = OutboxType.SendMessage,
                        Payload = new SendMessagePayload
                        {
                            UserId = user1.Id,
                            Message = new TournamentDuelInvitationMessage
                            {
                                TournamentId = tournament.Id,
                                TournamentName = tournament.Name,
                                OpponentNickname = user2.Nickname,
                                ConfigurationId = tournament.DuelConfiguration?.Id
                            }
                        },
                        RetryUntil = DateTime.UtcNow.AddMinutes(5)
                    },
                    new OutboxMessage
                    {
                        Type = OutboxType.SendMessage,
                        Payload = new SendMessagePayload
                        {
                            UserId = user2.Id,
                            Message = new TournamentDuelInvitationMessage
                            {
                                TournamentId = tournament.Id,
                                TournamentName = tournament.Name,
                                OpponentNickname = user1.Nickname,
                                ConfigurationId = tournament.DuelConfiguration?.Id
                            }
                        },
                        RetryUntil = DateTime.UtcNow.AddMinutes(5)
                    });

                tournamentPendingDuels.Add(new TournamentPendingDuel
                {
                    Type = PendingDuelType.Tournament,
                    Tournament = tournament,
                    User1 = user1,
                    User2 = user2,
                    Configuration = tournament.DuelConfiguration,
                    CreatedAt = DateTime.UtcNow
                });
                busyUserIds.Add(user1.Id);
                busyUserIds.Add(user2.Id);

                logger.LogInformation(
                    "Tournament duel queued. TournamentId = {TournamentId}, Users = {User1}, {User2}",
                    tournament.Id,
                    user1.Id,
                    user2.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<HashSet<int>> GetPendingUserIdsAsync(CancellationToken cancellationToken)
    {
        var userIds = new HashSet<int>();

        var rankedUsers = await context.PendingDuels
            .OfType<RankedPendingDuel>()
            .AsNoTracking()
            .Select(d => d.User.Id)
            .ToListAsync(cancellationToken);
        userIds.UnionWith(rankedUsers);

        var friendlyUsers = await context.PendingDuels
            .OfType<FriendlyPendingDuel>()
            .AsNoTracking()
            .Select(d => d.User1.Id)
            .Concat(context.PendingDuels
                .OfType<FriendlyPendingDuel>()
                .AsNoTracking()
                .Select(d => d.User2.Id))
            .ToListAsync(cancellationToken);
        userIds.UnionWith(friendlyUsers);

        var groupUsers = await context.PendingDuels
            .OfType<GroupPendingDuel>()
            .AsNoTracking()
            .Select(d => d.User1.Id)
            .Concat(context.PendingDuels
                .OfType<GroupPendingDuel>()
                .AsNoTracking()
                .Select(d => d.User2.Id))
            .ToListAsync(cancellationToken);
        userIds.UnionWith(groupUsers);

        var tournamentUsers = await context.PendingDuels
            .OfType<TournamentPendingDuel>()
            .AsNoTracking()
            .Select(d => d.User1.Id)
            .Concat(context.PendingDuels
                .OfType<TournamentPendingDuel>()
                .AsNoTracking()
                .Select(d => d.User2.Id))
            .ToListAsync(cancellationToken);
        userIds.UnionWith(tournamentUsers);

        return userIds;
    }
}
