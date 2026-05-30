namespace Duely.Domain.Common.Entities;

#pragma warning disable CA1040
public interface IIdentity;
#pragma warning restore CA1040

public abstract record Identity<TId> : IComparable<Identity<TId>>, IIdentity
    where TId : IEquatable<TId>, IComparable<TId>
{
    protected Identity(TId value)
    {
        if (EqualityComparer<TId>.Default.Equals(value, y: default))
        {
            throw new ArgumentException($"'{nameof(value)}' cannot be default value.", nameof(value));
        }

        Value = value;
    }

    public TId Value { get; }

    public int CompareTo(Identity<TId>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return other is null
            ? 1
            : Value.CompareTo(other.Value);
    }

    public override string ToString() => $"{Value}";

    public static implicit operator TId(Identity<TId> identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return identity.Value;
    }

    public static bool operator <(Identity<TId>? left, Identity<TId>? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator >(Identity<TId>? left, Identity<TId>? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator <=(Identity<TId>? left, Identity<TId>? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Identity<TId>? left, Identity<TId>? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }
}
