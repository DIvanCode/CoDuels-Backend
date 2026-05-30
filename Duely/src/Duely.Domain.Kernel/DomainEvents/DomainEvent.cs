using MediatR;

namespace Duely.Domain.Common.DomainEvents;

public interface IDomainEvent : INotification;

public abstract class DomainEvent : IDomainEvent;
