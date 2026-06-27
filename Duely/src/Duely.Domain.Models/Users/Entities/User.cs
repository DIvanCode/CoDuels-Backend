using Duely.Domain.Kernel.Entities;

namespace Duely.Domain.Models.Users.Entities;

public sealed class User : Entity
{
    public User(string nickname, DateTime createdAt, string passwordHash, string passwordSalt, int rating)
    {
        Nickname = nickname;
        CreatedAt = createdAt;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        Rating = rating;
    }
    
    public int Id { get; init; }
    public string Nickname { get; init; }
    public DateTime CreatedAt { get; init; }
    
    public string PasswordHash { get; init; }
    public string PasswordSalt { get; init; }
    
    public bool IsAdmin { get; init; }

    public string? RefreshToken { get; private set; }
    public string? IdentityTicket { get; private set; }
    
    public int Rating { get; private set; }
    
    public void UpdateRefreshToken(string refreshToken)
    {
        RefreshToken = refreshToken;
    }
    
    public void SetIdentityTicket(string identityTicket)
    {
        IdentityTicket = identityTicket;
    }
    
    public void ClearIdentityTicket()
    {
        IdentityTicket = null;
    }
}
