using Duely.Domain.Models;
using Microsoft.Extensions.Options;

namespace Duely.Domain.Services.Duels;
public interface IRatingManager
{
    void UpdateRatings(Duel duel);
    Dictionary<DuelResult, int> GetRatingChanges(Duel duel, int rating, int anotherRating);
    int GetTaskLevel(int rating);
}
public sealed class RatingManager(IOptions<DuelOptions> options) : IRatingManager
{
    public void UpdateRatings(Duel duel)
    {
        if (!duel.Configuration.IsRated)
        {
            duel.User1FinalRating = duel.User1InitRating;
            duel.User2FinalRating = duel.User2InitRating;
            return;
        }

        var rating1 = duel.User1InitRating;
        var rating2 = duel.User2InitRating;
        
        var ratingChanges1 = GetRatingChanges(duel, rating1, rating2);
        var ratingChanges2 = GetRatingChanges(duel, rating2, rating1);

        var result1 = duel.Winner == null
            ? DuelResult.Draw
            : duel.Winner!.Id == duel.User1.Id
                ? DuelResult.Win
                : DuelResult.Lose;
        var result2 = duel.Winner == null
            ? DuelResult.Draw
            : duel.Winner!.Id == duel.User2.Id
                ? DuelResult.Win
                : DuelResult.Lose;

        var newRating1 = rating1 + ratingChanges1[result1];
        var newRating2 = rating2 + ratingChanges2[result2];
        
        duel.User1FinalRating = newRating1;
        duel.User1.Rating = newRating1;
        
        duel.User2FinalRating = newRating2;
        duel.User2.Rating = newRating2;
    }

    public Dictionary<DuelResult, int> GetRatingChanges(Duel duel, int rating, int anotherRating)
    {
        if (!duel.Configuration.IsRated)
        {
            return new Dictionary<DuelResult, int>
            {
                [DuelResult.Win] = 0,
                [DuelResult.Draw] = 0,
                [DuelResult.Lose] = 0
            };
        }

        var expected = 1.0 / (1.0 + Math.Pow(10, (anotherRating - rating) / 400.0));
        var k = GetK(rating);

        return new Dictionary<DuelResult, int>
        {
            [DuelResult.Win] = (int)Math.Round(k * (1.0 - expected)),
            [DuelResult.Draw] = (int)Math.Round(k * (0.5 - expected)),
            [DuelResult.Lose] = (int)Math.Round(k * (0.0 - expected))
        };
    }

    public int GetTaskLevel(int rating)
    {
        var bestLevel = options.Value.RatingToTaskLevelMapping
            .Where(item => item.GetInterval().MinRating <= rating && rating <= item.GetInterval().MaxRating)
            .Select(item => item.Level)
            .SingleOrDefault();
        return bestLevel == default ? 1 : bestLevel;
    }
    
    private static int GetK(int rating)
    {
        return rating switch
        {
            < 1600 => 40,
            < 2000 => 32,
            < 2200 => 24,
            _ => 16
        };
    }
}
