namespace Duely.Domain.Services.Duels;

public interface IDuelManager
{
    void AddUser(int userId, int rating);
    (int User1, int User2)? TryGetPair();
}

public sealed class DuelManager : IDuelManager
{
    private sealed class WaitingUser
    {
        public required int UserId { get; init; }
        public required int Rating { get; init; }
        public required DateTime EnqueuedAt { get; init; }
    }

    private readonly List<WaitingUser> _waitingUsers = new();

    private const int BaseWindow = 50;
    private const int GrowPerSecond = 5;

    private const int FallbackAfterSeconds = 120;

    public void AddUser(int userId, int rating)
        => AddUser(userId, rating, DateTime.UtcNow);
    public void AddUser(int userId, int rating, DateTime enqueuedAt)
    {
        if (_waitingUsers.Any(u => u.UserId == userId))
            return;

        _waitingUsers.Add(new WaitingUser
        {
            UserId = userId,
            Rating = rating,
            EnqueuedAt = enqueuedAt
        });
    }

    public (int User1, int User2)? TryGetPair()
    {
        if (_waitingUsers.Count < 2)
            return null;

        var now = DateTime.UtcNow;
        var sorted = _waitingUsers
            .OrderBy(u => u.Rating)
            .ToList();
            
        WaitingUser? bestA = null;
        WaitingUser? bestB = null;
        int bestDiff = int.MaxValue;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            int diff = Math.Abs(a.Rating - b.Rating);
            int allowed = Math.Min(GetWindowFor(a, now), GetWindowFor(b, now));

            if (diff > allowed)
                continue;
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestA = a;
                bestB = b;
            }
            else if (diff == bestDiff && bestA != null && bestB != null)
            {
                var prevMinWait = Math.Min(
                    (now - bestA.EnqueuedAt).TotalSeconds,
                    (now - bestB.EnqueuedAt).TotalSeconds);

                var newMinWait = Math.Min(
                    (now - a.EnqueuedAt).TotalSeconds,
                    (now - b.EnqueuedAt).TotalSeconds);

                if (newMinWait > prevMinWait)
                {
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA != null && bestB != null)
        {
            _waitingUsers.Remove(bestA);
            _waitingUsers.Remove(bestB);
            return (bestA.UserId, bestB.UserId);
        }
        var oldestWaitSeconds = (now - _waitingUsers.Min(u => u.EnqueuedAt)).TotalSeconds;
        if (oldestWaitSeconds < FallbackAfterSeconds)
            return null;
        WaitingUser? fbA = null;
        WaitingUser? fbB = null;
        int fbBestDiff = int.MaxValue;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            int diff = Math.Abs(a.Rating - b.Rating);

            if (diff < fbBestDiff)
            {
                fbBestDiff = diff;
                fbA = a;
                fbB = b;
            }
            else if (diff == fbBestDiff && fbA != null && fbB != null)
            {
                var prevMinWait = Math.Min(
                    (now - fbA.EnqueuedAt).TotalSeconds,
                    (now - fbB.EnqueuedAt).TotalSeconds);

                var newMinWait = Math.Min(
                    (now - a.EnqueuedAt).TotalSeconds,
                    (now - b.EnqueuedAt).TotalSeconds);

                if (newMinWait > prevMinWait)
                {
                    fbA = a;
                    fbB = b;
                }
            }
        }

        if (fbA == null || fbB == null)
            return null;

        _waitingUsers.Remove(fbA);
        _waitingUsers.Remove(fbB);
        return (fbA.UserId, fbB.UserId);
    }

    private static int GetWindowFor(WaitingUser user, DateTime now)
    {
        var seconds = (now - user.EnqueuedAt).TotalSeconds;
        return BaseWindow + (int)(seconds * GrowPerSecond);
    }
}
