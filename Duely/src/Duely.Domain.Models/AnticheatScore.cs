using Duely.Domain.Models.Duels;

namespace Duely.Domain.Models;

public sealed class AnticheatScore
{
    public required Duel Duel { get; init; }
    public required User User { get; init; }
    public required char TaskKey { get; init; }
    public float? Score { get; set; }
}
