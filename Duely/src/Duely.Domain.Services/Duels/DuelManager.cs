using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;

namespace Duely.Domain.Services.Duels;

public interface IDuelManager
{
    IEnumerable<DuelPair> GetPairs(List<PendingDuel> pendingDuels);
}

public sealed record DuelPair(
    User User1,
    User User2,
    DuelConfiguration? Configuration,
    bool IsRated,
    List<PendingDuel> UsedPendingDuels);

public sealed class DuelManager : IDuelManager
{
    private const int MaxWaitSeconds = 20;

    public IEnumerable<DuelPair> GetPairs(List<PendingDuel> pendingDuels)
    {
        if (pendingDuels.Count < 1)
        {
            return Array.Empty<DuelPair>();
        }

        var pairs = new List<DuelPair>();
        var usedUsers = new HashSet<int>();

        foreach (var duel in pendingDuels.OfType<FriendlyPendingDuel>().OrderBy(p => p.Id))
        {
            if (!duel.IsAccepted)
            {
                continue;
            }

            if (usedUsers.Contains(duel.User1.Id) || usedUsers.Contains(duel.User2.Id))
            {
                continue;
            }

            pairs.Add(new DuelPair(
                duel.User1,
                duel.User2,
                duel.Configuration,
                duel.Configuration?.IsRated ?? false,
                [duel]));

            usedUsers.Add(duel.User1.Id);
            usedUsers.Add(duel.User2.Id);
        }

        foreach (var duel in pendingDuels.OfType<GroupPendingDuel>().OrderBy(p => p.Id))
        {
            if (!duel.IsAcceptedByUser1 || !duel.IsAcceptedByUser2)
            {
                continue;
            }

            if (usedUsers.Contains(duel.User1.Id) || usedUsers.Contains(duel.User2.Id))
            {
                continue;
            }

            pairs.Add(new DuelPair(
                duel.User1,
                duel.User2,
                duel.Configuration,
                duel.Configuration?.IsRated ?? false,
                [duel]));

            usedUsers.Add(duel.User1.Id);
            usedUsers.Add(duel.User2.Id);
        }

        foreach (var duel in pendingDuels.OfType<TournamentPendingDuel>().OrderBy(p => p.Id))
        {
            if (!duel.IsAcceptedByUser1 || !duel.IsAcceptedByUser2)
            {
                continue;
            }

            if (usedUsers.Contains(duel.User1.Id) || usedUsers.Contains(duel.User2.Id))
            {
                continue;
            }

            pairs.Add(new DuelPair(
                duel.User1,
                duel.User2,
                duel.Configuration,
                duel.Configuration?.IsRated ?? false,
                [duel]));

            usedUsers.Add(duel.User1.Id);
            usedUsers.Add(duel.User2.Id);
        }

        var candidates = pendingDuels
            .OfType<RankedPendingDuel>()
            .Where(p => !usedUsers.Contains(p.User.Id))
            .ToList();
        var lateCandidates = candidates
            .Where(p => !p.User.IsBot && (DateTime.UtcNow - p.CreatedAt).TotalSeconds > MaxWaitSeconds)
            .ToList();
        (RankedPendingDuel A, RankedPendingDuel B)? pair = null;
        if (lateCandidates.Count != 0)
        {
            var user = lateCandidates
                .OrderBy(p => p.CreatedAt)
                .First();
            var bot = candidates
                .Where(p => p.User.IsBot)
                .OrderBy(p => Math.Abs(user.Rating - p.User.Rating))
                .FirstOrDefault();
            if (bot is not null)
            {
                pair = (user, bot);
            }
        }
        else
        {
            candidates = candidates.Where(d => !d.User.IsBot).ToList();
            pair = TryGetRatedDuelPair(candidates, DateTime.UtcNow);
        }

        if (pair is null)
        {
            return pairs;
        }

        var (a, b) = pair.Value;
        pairs.Add(new DuelPair(
            a.User,
            b.User,
            null,
            true,
            [a, b]));

        usedUsers.Add(a.User.Id);
        usedUsers.Add(b.User.Id);

        return pairs;
    }

    private static (RankedPendingDuel A, RankedPendingDuel B)? TryGetRatedDuelPair(
        List<RankedPendingDuel> candidates,
        DateTime now)
    {
        candidates = candidates
            .OrderBy(u => u.Rating)
            .ThenBy(u => u.CreatedAt)
            .ToList();

        RankedPendingDuel? bestA = null, bestB = null;
        for (var i = 0; i < candidates.Count - 1; i++)
        {
            var a = candidates[i];
            var b = candidates[i + 1];
            var diff = Math.Abs(a.Rating - b.Rating);
            var allowed = Math.Max(GetWindowFor(a, now), GetWindowFor(b, now));

            if (diff > allowed)
            {
                continue;
            }

            if (bestA is null || bestB is null)
            {
                bestA = a;
                bestB = b;
                continue;
            }
            
            var prevMaxWait = Math.Max(
                (now - bestA.CreatedAt).TotalSeconds,
                (now - bestB.CreatedAt).TotalSeconds);
            var newMaxWait = Math.Max(
                (now - a.CreatedAt).TotalSeconds,
                (now - b.CreatedAt).TotalSeconds);
            if (newMaxWait > prevMaxWait)
            {
                bestA = a;
                bestB = b;
            }
        }

        if (bestA is not null && bestB is not null)
        {
            return (bestA, bestB);
        }

        return null;
    }

    private static int GetWindowFor(RankedPendingDuel user, DateTime now)
    {
        var seconds = (now - user.CreatedAt).TotalSeconds;
        return (int)(15 * seconds);
    }
}
