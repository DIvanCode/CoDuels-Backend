using System;
using Duely.Domain.Models;

namespace Duely.Domain.Services.Duels;
public interface IRatingManager
{
    void UpdateRatings(Duel duel);
}
public sealed class RatingManager : IRatingManager
{
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

        var k1 = GetK(user1.Rating);
        var k2 = GetK(user2.Rating);

        var newRating1 = (int)Math.Round(r1 + k1 * (score1 - expected1));
        var newRating2 = (int)Math.Round(r2 + k2 * (score2 - expected2));

        duel.User1RatingDelta = newRating1 - user1.Rating;
        duel.User2RatingDelta = newRating2 - user2.Rating;

        user1.Rating = newRating1;
        user2.Rating = newRating2;
    }
    private static int GetK(int rating)
    {
        if (rating < 1600)
            return 40;

        if (rating < 2000)
            return 32;

        if (rating < 2200)
            return 24;

        return 16;
    }
}