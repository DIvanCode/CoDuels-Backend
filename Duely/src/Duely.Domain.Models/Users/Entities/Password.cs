using Duely.Domain.Common.Entities;

namespace Duely.Domain.Models.Users.Entities;

public sealed class Password : ValueObject
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    
    public Password(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(value.Length, MinLength, nameof(value.Length));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength, nameof(value.Length));
        
        Salt = Guid.NewGuid().ToString();
        Hash = BCrypt.Net.BCrypt.HashPassword(value + Salt, 12);
    }
    
    public string Hash { get; init; }
    public string Salt { get; init; }

    public bool Verify(string value)
    {
        return BCrypt.Net.BCrypt.Verify(value + Salt, Hash);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Hash;
    }
}
