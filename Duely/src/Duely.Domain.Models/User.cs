using System.ComponentModel.DataAnnotations.Schema;

namespace Duely.Domain.Models;

public sealed class User
{
    public int Id { get; init; }
    public required string Nickname { get; set; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public string? RefreshToken { get; set; }
    public int Rating { get; set; } = 1500;
    public required DateTime CreatedAt { get; init; }

    // Such strange approach is used because of navigation properties of EF Core
    // Do not use DuelsAsUser1 and DuelsAsUser2 explicitly
    public List<Duel> DuelsAsUser1 { get; } = [];
    public List<Duel> DuelsAsUser2 { get; } = [];
    [NotMapped]
    public IReadOnlyCollection<Duel> Duels => DuelsAsUser1.Concat(DuelsAsUser2).ToList();
}
