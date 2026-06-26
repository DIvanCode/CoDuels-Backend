using Duely.Domain.Kernel.Entities;
using Duely.Domain.Models.Users.DomainEvents;

namespace Duely.Domain.Models.Users.Entities;

public sealed class User : Entity
{
    private User(string nickname, DateTime createdAt, string passwordHash, string passwordSalt, int rating)
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

    public static User Create(string nickname, DateTime createdAt, string passwordHash, string passwordSalt, int rating)
    {
        var user = new User(nickname, createdAt, passwordHash, passwordSalt, rating);        
        user.AddDomainEvent(new UserCreatedDomainEvent(user));
        return user;
    }
    
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
