namespace Duely.Domain.Services;

public sealed class DuelManager
{
    private readonly List<string> _waitingUsers = new();

    public void AddUser(string userId)
    {
        if (!_waitingUsers.Contains(userId)) {
            _waitingUsers.Add(userId);
        }
        
    }

    public (string User1, string User2)? TryGetPair()
    {
        if (_waitingUsers.Count == 2) {
            var user1 = _waitingUsers[0];
            var user2 = _waitingUsers[1];

            _waitingUsers.Clear();
            return (user1, user2);
        }
        
        return null;
    }
}