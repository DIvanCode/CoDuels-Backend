namespace Duely.Domain.Services;

public interface IDuelManager
{
    void AddUser(int userId);
    (int User1, int User2)? TryGetPair();
}


public sealed class DuelManager: IDuelManager
{
    private readonly Queue<int> _waitingUsers = new();

    public void AddUser(int userId)
    {
        if (!_waitingUsers.Contains(userId)) {
            _waitingUsers.Enqueue(userId);
        }
        
    }

    public (int User1, int User2)? TryGetPair()
    {
        if (_waitingUsers.Count >= 2) {
            var user1 = _waitingUsers.Dequeue();
            var user2 = _waitingUsers.Dequeue();

            return (user1, user2);
        }
        
        return null;
    }
}