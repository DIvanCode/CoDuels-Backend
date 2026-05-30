using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Users.DomainEvents;

namespace Duely.Domain.Models.Users.Entities;

public sealed class User : Entity<UserId>
{
    public User(
        UserId id,
        Nickname nickname,
        Password password,
        DateTime createdAt,
        Rating rating) : base(id)
    {
        Nickname = nickname;
        Password = password;
        CreatedAt = createdAt;
        Rating = rating;
        
        AddDomainEvent(new UserCreatedDomainEvent(Id));
    }
    
    public Nickname Nickname { get; init; }
    public Password Password { get; init; }
    public DateTime CreatedAt { get; init; }
    
    public string? RefreshToken { get; private set; }
    public string? IdentityTicket { get; private set; }
    
    public Rating Rating { get; private set; }
    
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

public sealed record UserId(Guid Value) : Identity<Guid>(Value);
