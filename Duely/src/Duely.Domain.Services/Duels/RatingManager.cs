using System;
using Duely.Domain.Models;

namespace Duely.Domain.Services.Duels;
public interface IRatingManager
{
    void UpdateRatings(Duel duel);
}
public sealed class RatingManager : IRatingManager
{
    private const int K = 32;

    public void UpdateRatings(Duel duel)
    {
        var user1 = duel.User1;
        var user2 = duel.User2;

        double r1 = user1.Rating;
        double r2 = user2.Rating;

        double score1;
        double score2;
        
        if (duel.Winner is null)
        {
            score1 = 0.5;
            score2 = 0.5;
        }
        else if (duel.Winner.Id == user1.Id)
        {
            score1 = 1.0;
            score2 = 0.0;
        }
        else
        {
            score1 = 0.0;
            score2 = 1.0;
        }

        double expected1 = 1.0 / (1.0 + Math.Pow(10, (r2 - r1) / 400.0));
        double expected2 = 1.0 / (1.0 + Math.Pow(10, (r1 - r2) / 400.0));

        user1.Rating = (int)Math.Round(r1 + K * (score1 - expected1));
        user2.Rating = (int)Math.Round(r2 + K * (score2 - expected2));
    }
}