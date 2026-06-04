using Duely.Domain.Common.Entities;
using Duely.Domain.Models.Users.Entities;

namespace Duely.Domain.Models.Tournaments.Entities;

public sealed class TournamentParticipant : ValueObject
{
    public TournamentParticipant(Tournament tournament, User user)
    {
        Tournament = tournament;
        User = user;
        Seed = Random.Shared.Next();
    }
    
    public Tournament Tournament { get; init; }
    public User User { get; init; }
    public int Seed { get; init; }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Tournament.Id;
        yield return User.Id;
    }
}
