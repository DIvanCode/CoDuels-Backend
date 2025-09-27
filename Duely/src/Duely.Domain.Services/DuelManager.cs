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
}