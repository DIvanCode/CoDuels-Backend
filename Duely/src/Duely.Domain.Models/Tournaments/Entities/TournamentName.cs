using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Tournaments.Entities;

public sealed class TournamentName : ValueObject
{
    public const int MaxLength = 100; 
    
    public TournamentName(string value)
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
