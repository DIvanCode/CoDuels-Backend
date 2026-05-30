using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Groups.Entities;

public sealed class GroupName : ValueObject
{
    public const int MaxLength = 100; 
    
    public GroupName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength, nameof(value.Length));
        
        Value = value.Trim();
    }
    
    public string Value { get; init; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
