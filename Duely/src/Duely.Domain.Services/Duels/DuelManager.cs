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
    private const int BaseWindow = 50;
    private const int GrowPerSecond = 5;

    private const int FallbackAfterSeconds = 120;

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

        var candidates = pendingDuels
            .OfType<RankedPendingDuel>()
            .Where(p => !usedUsers.Contains(p.User.Id))
            .ToList();
        var pair = TryGetRatedDuelPair(candidates, DateTime.UtcNow);
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
        if (candidates.Count < 2)
        {
            return null;
        }

        var sorted = candidates
            .OrderBy(u => u.Rating)
            .ThenBy(u => u.CreatedAt)
            .ToList();

        RankedPendingDuel? bestA = null;
        RankedPendingDuel? bestB = null;
        var bestDiff = int.MaxValue;
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            var diff = Math.Abs(a.Rating - b.Rating);
            var allowed = Math.Min(GetWindowFor(a, now), GetWindowFor(b, now));

            if (diff > allowed)
            {
                continue;
            }

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestA = a;
                bestB = b;
            }
            else if (diff == bestDiff && bestA is not null && bestB is not null)
            {
                var prevMinWait = Math.Min(
                    (now - bestA.CreatedAt).TotalSeconds,
                    (now - bestB.CreatedAt).TotalSeconds);

                var newMinWait = Math.Min(
                    (now - a.CreatedAt).TotalSeconds,
                    (now - b.CreatedAt).TotalSeconds);

                if (newMinWait > prevMinWait)
                {
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA is not null && bestB is not null)
        {
            return (bestA, bestB);
        }

        var oldestWaitingUser = sorted.MinBy(u => u.CreatedAt);
        var oldestWaitSeconds = (now - oldestWaitingUser!.CreatedAt).TotalSeconds;
        if (oldestWaitSeconds < FallbackAfterSeconds)
        {
            return null;
        }

        RankedPendingDuel? fbA = null;
        RankedPendingDuel? fbB = null;
        var fbBestDiff = int.MaxValue;
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            var diff = Math.Abs(a.Rating - b.Rating);

            if (diff < fbBestDiff)
            {
                fbBestDiff = diff;
                fbA = a;
                fbB = b;
            }
            else if (diff == fbBestDiff && fbA is not null && fbB is not null)
            {
                var prevMinWait = Math.Min(
                    (now - fbA.CreatedAt).TotalSeconds,
                    (now - fbB.CreatedAt).TotalSeconds);

                var newMinWait = Math.Min(
                    (now - a.CreatedAt).TotalSeconds,
                    (now - b.CreatedAt).TotalSeconds);

                if (newMinWait > prevMinWait)
                {
                    fbA = a;
                    fbB = b;
                }
            }
        }

        return fbA is null || fbB is null ? null : (fbA, fbB);
    }

    private static int GetWindowFor(RankedPendingDuel user, DateTime now)
    {
        var seconds = (now - user.CreatedAt).TotalSeconds;
        return BaseWindow + (int)(seconds * GrowPerSecond);
    }
}
