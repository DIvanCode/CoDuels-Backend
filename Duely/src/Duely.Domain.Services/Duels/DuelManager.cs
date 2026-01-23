namespace Duely.Domain.Services.Duels;

public interface IDuelManager
{
    bool IsUserWaiting(int userId);
    bool TryGetWaitingUser(int userId, out WaitingUser? waitingUser);
    void AddUser(
        int userId,
        int rating,
        DateTime utcNow,
        int? expectedOpponentId = null,
        int? configurationId = null);
    void RemoveUser(int userId);
    bool TryRemoveInvitation(int inviterId, int expectedOpponentId);
    IReadOnlyCollection<WaitingUser> GetWaitingUsers();
    DuelPair? TryGetPair();
    int GetWaitingUsersCount();
}

public sealed record DuelPair(int User1, int User2, int? ConfigurationId);

public sealed class WaitingUser
{
    public required int UserId { get; init; }
    public required int Rating { get; init; }
    public required DateTime EnqueuedAt { get; init; }
    public required int? ExpectedOpponentId { get; init; }
    public required int? ConfigurationId { get; init; }
    public required bool IsOpponentAssigned { get; init; }
}

public sealed class DuelManager : IDuelManager
{
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

    public bool TryGetWaitingUser(int userId, out WaitingUser? waitingUser)
    {
        lock (_waitingUsers)
        {
            if (!_waitingUsers.TryGetValue(userId, out var user))
            {
                waitingUser = null;
                return false;
            }

            waitingUser = new WaitingUser
            {
                UserId = user.UserId,
                Rating = user.Rating,
                EnqueuedAt = user.EnqueuedAt,
                ExpectedOpponentId = user.ExpectedOpponentId,
                ConfigurationId = user.ConfigurationId,
                IsOpponentAssigned = user.IsOpponentAssigned
            };
            return true;
        }
    }
    
    public void AddUser(
        int userId,
        int rating,
        DateTime utcNow,
        int? expectedOpponentId = null,
        int? configurationId = null)
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
                EnqueuedAt = utcNow,
                ExpectedOpponentId = expectedOpponentId,
                ConfigurationId = configurationId,
                IsOpponentAssigned = false
            };

            if (expectedOpponentId is null ||
                !_waitingUsers.TryGetValue(expectedOpponentId.Value, out var opponent) ||
                opponent.ExpectedOpponentId != userId)
            {
                return;
            }

            _waitingUsers[userId] = new WaitingUser
            {
                UserId = userId,
                Rating = rating,
                EnqueuedAt = utcNow,
                ExpectedOpponentId = expectedOpponentId,
                ConfigurationId = configurationId,
                IsOpponentAssigned = true
            };

            _waitingUsers[opponent.UserId] = new WaitingUser
            {
                UserId = opponent.UserId,
                Rating = opponent.Rating,
                EnqueuedAt = opponent.EnqueuedAt,
                ExpectedOpponentId = opponent.ExpectedOpponentId,
                ConfigurationId = opponent.ConfigurationId,
                IsOpponentAssigned = true
            };    
        }
    }
    
    public void RemoveUser(int userId)
    {
        lock (_waitingUsers)
        {
            if (!_waitingUsers.TryGetValue(userId, out var user))
            {
                return;
            }

            if (user.IsOpponentAssigned &&
                user.ExpectedOpponentId is not null &&
                _waitingUsers.TryGetValue(user.ExpectedOpponentId.Value, out var opponent) &&
                opponent.ExpectedOpponentId == userId)
            {
                _waitingUsers[opponent.UserId] = new WaitingUser
                {
                    UserId = opponent.UserId,
                    Rating = opponent.Rating,
                    EnqueuedAt = opponent.EnqueuedAt,
                    ExpectedOpponentId = opponent.ExpectedOpponentId,
                    ConfigurationId = opponent.ConfigurationId,
                    IsOpponentAssigned = false
                };
            }
            
            _waitingUsers.Remove(userId);
        }
    }

    public bool TryRemoveInvitation(int inviterId, int expectedOpponentId)
    {
        lock (_waitingUsers)
        {
            if (!_waitingUsers.TryGetValue(inviterId, out var user))
            {
                return false;
            }

            if (user.ExpectedOpponentId != expectedOpponentId)
            {
                return false;
            }

            _waitingUsers.Remove(inviterId);
            return true;
        }
    }

    public DuelPair? TryGetPair()
    {
        lock (_waitingUsers)
        {
            if (_waitingUsers.Count < 2)
            {
                return null;   
            }

            var invitedPair = TryGetInvitedPair();
            if (invitedPair is not null)
            {
                return invitedPair;
            }

            var now = DateTime.UtcNow;
            var sorted = _waitingUsers
                .Select(p => p.Value)
                .Where(u => u.ExpectedOpponentId is null)
                .OrderBy(u => u.Rating)
                .ToList();
            if (sorted.Count < 2)
            {
                return null;
            }
                
            WaitingUser? bestA = null;
            WaitingUser? bestB = null;
            var bestDiff = int.MaxValue;
            for (var i = 0; i < sorted.Count - 1; i++)
            {
                var a = sorted[i];
                var b = sorted[i + 1];
                if (!AreConfigurationsCompatible(a, b))
                {
                    continue;
                }
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
                _waitingUsers.Remove(bestA.UserId);
                _waitingUsers.Remove(bestB.UserId);
                return new DuelPair(
                    bestA.UserId,
                    bestB.UserId,
                    bestA.ConfigurationId ?? bestB.ConfigurationId);
            }

            var oldestWaitingUser = sorted.MinBy(u => u.EnqueuedAt);
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
                if (!AreConfigurationsCompatible(a, b))
                {
                    continue;
                }
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
            
            return new DuelPair(
                fbA.UserId,
                fbB.UserId,
                fbA.ConfigurationId ?? fbB.ConfigurationId);
        }
    }

    public IReadOnlyCollection<WaitingUser> GetWaitingUsers()
    {
        lock (_waitingUsers)
        {
            return _waitingUsers.Values
                .Select(user => new WaitingUser
                {
                    UserId = user.UserId,
                    Rating = user.Rating,
                    EnqueuedAt = user.EnqueuedAt,
                    ExpectedOpponentId = user.ExpectedOpponentId,
                    ConfigurationId = user.ConfigurationId,
                    IsOpponentAssigned = user.IsOpponentAssigned
                })
                .ToList()
                .AsReadOnly();
        }
    }
    public int GetWaitingUsersCount()
    {
        lock (_waitingUsers)
        {
            return _waitingUsers.Count;
        }
    }


    private static int GetWindowFor(WaitingUser user, DateTime now)
    {
        var seconds = (now - user.EnqueuedAt).TotalSeconds;
        return BaseWindow + (int)(seconds * GrowPerSecond);
    }

    private DuelPair? TryGetInvitedPair()
    {
        var invitedPairs = new List<(WaitingUser A, WaitingUser B, DateTime Oldest)>();

        foreach (var user in _waitingUsers.Values)
        {
            if (user.ExpectedOpponentId is null)
            {
                continue;
            }

            if (!_waitingUsers.TryGetValue(user.ExpectedOpponentId.Value, out var opponent))
            {
                continue;
            }

            if (opponent.ExpectedOpponentId != user.UserId)
            {
                continue;
            }

            if (user.UserId > opponent.UserId)
            {
                continue;
            }

            var oldest = user.EnqueuedAt <= opponent.EnqueuedAt ? user.EnqueuedAt : opponent.EnqueuedAt;
            invitedPairs.Add((user, opponent, oldest));
        }

        if (invitedPairs.Count == 0)
        {
            return null;
        }

        var pair = invitedPairs
            .OrderBy(p => p.Oldest)
            .First();

        _waitingUsers.Remove(pair.A.UserId);
        _waitingUsers.Remove(pair.B.UserId);
        var primary = pair.A.EnqueuedAt <= pair.B.EnqueuedAt ? pair.A : pair.B;
        var secondary = ReferenceEquals(primary, pair.A) ? pair.B : pair.A;
        return new DuelPair(
            pair.A.UserId,
            pair.B.UserId,
            primary.ConfigurationId ?? secondary.ConfigurationId);
    }

    private static bool AreConfigurationsCompatible(WaitingUser a, WaitingUser b)
    {
        return a.ConfigurationId is null ||
               b.ConfigurationId is null ||
               a.ConfigurationId == b.ConfigurationId;
    }
}
