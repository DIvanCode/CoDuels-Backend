namespace Duely.Domain.Services;

public interface IDuelManager
{
    void AddUser(string userId);
    (string User1, string User2)? TryGetPair();
}


public sealed class DuelManager: IDuelManager
{
    private readonly Queue<string> _waitingUsers = new();

    public void AddUser(string userId)
    {
        if (!_waitingUsers.Contains(userId)) {
            _waitingUsers.Enqueue(userId);
        }
        
    }

    public (string User1, string User2)? TryGetPair()
    {
        if (_waitingUsers.Count >= 2) {
            var user1 = _waitingUsers.Dequeue();
            var user2 = _waitingUsers.Dequeue();

            return (user1, user2);
        }
        
        return null;
    }
}