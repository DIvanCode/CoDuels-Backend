using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Users.DomainEvents;

public sealed class UserCreatedDomainEvent(User user) : DomainEvent
{
    public User User { get; init; } = user;
}
