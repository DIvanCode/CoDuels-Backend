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

    public void AddUser(int userId)
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
        var oldestUser = _waitingUsers
            .OrderBy(u => u.EnqueuedAt)
            .First();
        var bestMatch = _waitingUsers
            .Where(u => u.UserId != oldestUser.UserId)
            .OrderBy(u => Math.Abs(u.Rating - oldestUser.Rating))
            .ThenBy(u => u.EnqueuedAt)                       
            .First();

        _waitingUsers.Remove(oldestUser);
        _waitingUsers.Remove(bestMatch);

        return (oldestUser.UserId, bestMatch.UserId);
    }
}
