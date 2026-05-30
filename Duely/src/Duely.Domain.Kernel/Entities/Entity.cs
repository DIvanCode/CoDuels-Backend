using System.ComponentModel;
using Duely.Domain.Common.DomainEvents;

namespace Duely.Domain.Common.Entities;

public interface IEntity
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public abstract class Entity<TId> : IEntity
    where TId : IEquatable<TId>, IComparable<TId>, IIdentity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _domainEvents.Add(domainEvent);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Id.Equals(default) || other.Id.Equals(default))
        {
            return false;
        }

        return Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }

    // ReSharper disable once UnusedMember.Local
#pragma warning disable CS8618, CS9264
    /// <summary>
    /// EF constructor. Do not use explicitly!
    /// </summary>
    [Obsolete(message: "For EF. Do not use explicitly!", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private Entity()
    {
    }
#pragma warning restore CS8618, CS9264
}
