using MediatR;

namespace Duely.Domain.Kernel.DomainEvents;

public interface IDomainEvent : INotification;

public abstract class DomainEvent : IDomainEvent;
