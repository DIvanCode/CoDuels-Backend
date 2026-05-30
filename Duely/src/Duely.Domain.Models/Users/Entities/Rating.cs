using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Users.Entities;

public sealed class Rating : ValueObject
{
    public Rating(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
        
        Value = value;
    }
    
    public int Value { get; init; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
