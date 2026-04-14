namespace Duely.Domain.Models;

public sealed class AnticheatScore
{
    public required int DuelId { get; init; }
    public required int UserId { get; init; }
    public required char TaskKey { get; init; }
    public float? Score { get; set; }
}
