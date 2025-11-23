using System;
using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Services;

public class RatingManagerTests
{
    private readonly RatingManager _ratingManager = new();

    private static User CreateUser(int id, int rating)
    {
        return new User
        {
            Id = id,
            Nickname = $"user{id}",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = rating
        };
    }

    private static Duel CreateDuel(int id, User u1, User u2)
    {
        return new Duel
        {
            Id = id,
            TaskId = "TASK",
            User1 = u1,
            User2 = u2,
            Status = DuelStatus.Finished,
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            EndTime = DateTime.UtcNow
        };
    }

    [Fact]
    public void Winner_gets_more_rating_and_loser_loses_for_equal_ratings()
    {
        var user1 = CreateUser(1, 1500);
        var user2 = CreateUser(2, 1500);
        var duel = CreateDuel(1, user1, user2);
        duel.Winner = user1;

        _ratingManager.UpdateRatings(duel);

        user1.Rating.Should().Be(1520); 
        user2.Rating.Should().Be(1480);  
    }

    [Fact]
    public void Draw_does_not_change_ratings_for_equal_ratings()
    {
        var user1 = CreateUser(1, 1500);
        var user2 = CreateUser(2, 1500);
        var duel = CreateDuel(1, user1, user2);
        duel.Winner = null; 
        _ratingManager.UpdateRatings(duel);

        user1.Rating.Should().Be(1500);
        user2.Rating.Should().Be(1500);
    }

    [Fact]
    public void Upset_win_gives_expected_change_for_lower_rated_player()
    {
        var user1 = CreateUser(1, 1400); 
        var user2 = CreateUser(2, 1600); 
        var duel = CreateDuel(1, user1, user2);
        duel.Winner = user1;

        _ratingManager.UpdateRatings(duel);

        user1.Rating.Should().Be(1430); 
        user2.Rating.Should().Be(1576);    
    }
    [Fact]
    public void Favorite_win_changes_ratings_less_than_upset()
    {
        var strong = CreateUser(1, 1600);
        var weak   = CreateUser(2, 1400);
        var duel = CreateDuel(1, strong, weak);
        duel.Winner = strong;

        _ratingManager.UpdateRatings(duel);

        strong.Rating.Should().Be(1608);  
        weak.Rating.Should().Be(1390);   
    }
}
