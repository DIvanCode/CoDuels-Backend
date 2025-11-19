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

    public void AddUser(int userId, int rating)
    {
        if (_waitingUsers.Any(u => u.UserId == userId))
        {
            return;
        }

        _waitingUsers.Add(new WaitingUser
        {
            UserId = userId,
            Rating = rating,
            EnqueuedAt = DateTime.UtcNow
        });
    }

    public (int User1, int User2)? TryGetPair()
    {
        if (_waitingUsers.Count < 2)
        {
            return null;
        }
        var now = DateTime.UtcNow;
        var sorted = _waitingUsers
            .OrderBy(u => u.Rating)
            .ThenBy(u => u.EnqueuedAt)
            .ToList();
        double bestScore = double.MaxValue;
        WaitingUser? bestA = null;
        WaitingUser? bestB = null;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            var score = CalculatePairScore(a, b, now);
            if (score < bestScore)
            {
                bestScore = score;
                bestA = a;
                bestB = b;
            }
        }
        if (bestA is null || bestB is null)
            return null;

        _waitingUsers.Remove(bestA);
        _waitingUsers.Remove(bestB);

        return (bestA.UserId, bestB.UserId);
    }

    private static double CalculatePairScore(WaitingUser a, WaitingUser b, DateTime now)
    {
        var ratingDiff = Math.Abs(a.Rating - b.Rating);
        var waitingA = (now - a.EnqueuedAt).TotalSeconds;
        var waitingB = (now - b.EnqueuedAt).TotalSeconds;
        var minWaiting = Math.Min(waitingA, waitingB);
        const double ratingWeight = 1.0;
        const double waitingWeight = 0.01; 
        return ratingWeight * ratingDiff - waitingWeight * minWaiting;
    }
}