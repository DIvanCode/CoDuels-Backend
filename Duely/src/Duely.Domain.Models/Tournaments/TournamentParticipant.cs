namespace Duely.Domain.Models.Tournaments;

public sealed class TournamentParticipant
{
    public required Tournament Tournament { get; init; }
    public required User User { get; init; }
    public required int Seed { get; init; }
}
