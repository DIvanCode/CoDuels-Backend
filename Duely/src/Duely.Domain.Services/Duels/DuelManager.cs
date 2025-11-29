namespace Duely.Domain.Services.Duels;

public interface IDuelManager
{
    bool IsUserWaiting(int userId);
    void AddUser(int userId, int rating, DateTime utcNow);
    void RemoveUser(int userId);
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

    private readonly Dictionary<int, WaitingUser> _waitingUsers = new();

    private const int BaseWindow = 50;
    private const int GrowPerSecond = 5;

    private const int FallbackAfterSeconds = 120;

    public bool IsUserWaiting(int userId)
    {
        lock (_waitingUsers)
        {
            return _waitingUsers.ContainsKey(userId);   
        }
    }
    
    public void AddUser(int userId, int rating, DateTime utcNow)
    {
        lock (_waitingUsers)
        {
            if (_waitingUsers.ContainsKey(userId))
            {
                return;
            }

            _waitingUsers[userId] = new WaitingUser
            {
                UserId = userId,
                Rating = rating,
                EnqueuedAt = utcNow
            };    
        }
    }
    
    public void RemoveUser(int userId)
    {
        lock (_waitingUsers)
        {
            if (!_waitingUsers.ContainsKey(userId))
            {
                return;
            }
            
            _waitingUsers.Remove(userId);
        }
    }

    public (int User1, int User2)? TryGetPair()
    {
        lock (_waitingUsers)
        {
            if (_waitingUsers.Count < 2)
            {
                return null;   
            }

            var now = DateTime.UtcNow;
            var sorted = _waitingUsers
                .Select(p => p.Value)
                .OrderBy(u => u.Rating)
                .ToList();
                
            WaitingUser? bestA = null;
            WaitingUser? bestB = null;
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
                lock (_waitingUsers)
                {
                    
                }
                _waitingUsers.Remove(bestA.UserId);
                _waitingUsers.Remove(bestB.UserId);
                return (bestA.UserId, bestB.UserId);
            }

            var oldestWaitingUser = _waitingUsers
                .Select(p => p.Value)
                .MinBy(u => u.EnqueuedAt);
            var oldestWaitSeconds = (now - oldestWaitingUser!.EnqueuedAt).TotalSeconds;
            if (oldestWaitSeconds < FallbackAfterSeconds)
            {
                return null;   
            }
            
            WaitingUser? fbA = null;
            WaitingUser? fbB = null;
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
            {
                return null;   
            }

            _waitingUsers.Remove(fbA.UserId);
            _waitingUsers.Remove(fbB.UserId);
            
            return (fbA.UserId, fbB.UserId);   
        }
    }

    private static int GetWindowFor(WaitingUser user, DateTime now)
    {
        var seconds = (now - user.EnqueuedAt).TotalSeconds;
        return BaseWindow + (int)(seconds * GrowPerSecond);
    }
}
