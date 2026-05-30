namespace Duely.Domain.Common.Entities;

public abstract class ValueObject : IEquatable<ValueObject>
{
    public bool Equals(ValueObject? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        if (ReferenceEquals(obj, this))
        {
            return true;
        }

        var other = (ValueObject)obj;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public static bool operator ==(ValueObject? one, ValueObject? two)
    {
        return EqualOperator(one, two);
    }

    public static bool operator !=(ValueObject? one, ValueObject? two)
    {
        return !EqualOperator(one, two);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var i in GetEqualityComponents())
        {
            hash.Add(i);
        }

        return hash.ToHashCode();
    }

    protected static bool EqualOperator(ValueObject? left, ValueObject? right)
    {
        if (left is null ^ right is null)
        {
            return false;
        }

        return ReferenceEquals(left, right) || left!.Equals(right);
    }

    protected abstract IEnumerable<object?> GetEqualityComponents();
}