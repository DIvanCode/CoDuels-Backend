using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Pending;
using Duely.Domain.Models.Groups;
using Duely.Domain.Models.Tournaments;

namespace Duely.Domain.Services.Tournaments;

public sealed record TournamentPendingDuelCandidate(int User1Id, int User2Id);

public interface ITournamentMatchmakingStrategy
{
    TournamentMatchmakingType Type { get; }
    Tournament CreateTournament(
        string name,
        Group group,
        User createdBy,
        DateTime createdAt,
        DuelConfiguration? duelConfiguration,
        IReadOnlyList<User> participants);
    void Initialize(Tournament tournament);
    IReadOnlyCollection<int> GetReferencedDuelIds(Tournament tournament);
    void Sync(Tournament tournament, IReadOnlyDictionary<int, Duel> duelsById);
    IReadOnlyList<TournamentPendingDuelCandidate> GetPendingDuelCandidates(
        Tournament tournament,
        IReadOnlyDictionary<int, Duel> duelsById);
    bool HasPendingDuel(
        Tournament tournament,
        TournamentPendingDuel pendingDuel,
        TournamentPendingDuelCandidate candidate);
    void AttachDuel(
        Tournament tournament,
        TournamentPendingDuel pendingDuel,
        Duel duel);
}
