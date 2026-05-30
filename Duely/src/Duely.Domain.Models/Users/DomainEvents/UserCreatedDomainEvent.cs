using Duely.Domain.Common.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Users.DomainEvents;

public sealed class UserCreatedDomainEvent : DomainEvent
{
    public UserCreatedDomainEvent(UserId id)
    {
        Id = id;
    }
    
    public UserId Id { get; init; }
}
