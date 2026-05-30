using System.Text.RegularExpressions;
using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Users.Entities;

public sealed class Nickname : ValueObject
{
    public const int MaxLength = 128;
    public static readonly Regex Regex = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    
    public Nickname(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength, nameof(value.Length));

        if (!Regex.IsMatch(value))
        {
            throw new ArgumentException($"{nameof(value)} must match regex.");
        }
        
        Value = value.Trim();
        LowerValue = Value.ToLowerInvariant();
    }
    
    public string Value { get; init; }
    public string LowerValue { get; init; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LowerValue;
    }
    
    public override string ToString() => Value;
}
