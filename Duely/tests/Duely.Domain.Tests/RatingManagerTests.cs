using Duely.Domain.Models;
using Duely.Domain.Services.Duels;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Duely.Domain.Tests;

public class RatingManagerTests
{
    private readonly RatingManager _ratingManager = new(
        Options.Create(new DuelOptions
        {
            DefaultMaxDurationMinutes = 30,
            RatingToTaskLevelMapping =
            [
                new RatingToTaskLevelMappingItem
                {
                    Rating = "0-1599",
                    Level = 1
                }
            ]
        }));

    private static User CreateUser(int id, int rating)
    {
        return new User
        {
            Id = id,
            Nickname = $"user{id}",
            PasswordSalt = "salt",
            PasswordHash = "hash",
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Duel CreateDuel(int id, User u1, User u2)
    {
        const char taskKey = 'A';
        var configuration = new DuelConfiguration
        {
            Id = id,
            Owner = u1,
            MaxDurationMinutes = 30,
            IsRated = true,
            ShouldShowOpponentCode = false,
            TasksCount = 1,
            TasksOrder = DuelTasksOrder.Sequential,
            TasksConfigurations = new Dictionary<char, DuelTaskConfiguration>
            {
                [taskKey] = new()
                {
                    Level = 1,
                    Topics = []
                }
            }
        };
        
        return new Duel
        {
            Id = id,
            Configuration = configuration,
            Status = DuelStatus.Finished,
            Tasks = new Dictionary<char, DuelTask>
            {
                [taskKey] = new("TASK", 1, [])
            },
            StartTime = DateTime.UtcNow,
            DeadlineTime = DateTime.UtcNow.AddMinutes(30),
            EndTime = DateTime.UtcNow,
            User1 = u1,
            User1InitRating = u1.Rating,
            User2 = u2,
            User2InitRating = u2.Rating
        };
    }

    [Fact]
    public void Unrated_duel_does_not_change_ratings_and_returns_zero_changes()
    {
        var user1 = CreateUser(1, 1500);
        var user2 = CreateUser(2, 1500);
        var duel = CreateDuel(1, user1, user2);
        duel.Configuration.IsRated = false;
        duel.Winner = user1;

        var changes = _ratingManager.GetRatingChanges(duel, user1.Rating, user2.Rating);

        changes[DuelResult.Win].Should().Be(0);
        changes[DuelResult.Draw].Should().Be(0);
        changes[DuelResult.Lose].Should().Be(0);

        _ratingManager.UpdateRatings(duel);

        user1.Rating.Should().Be(1500);
        user2.Rating.Should().Be(1500);
        duel.User1FinalRating.Should().Be(1500);
        duel.User2FinalRating.Should().Be(1500);
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

    [Fact]
    public void Winner_user2_updates_both_ratings()
    {
        var user1 = CreateUser(1, 1500);
        var user2 = CreateUser(2, 1500);
        var duel = CreateDuel(1, user1, user2);
        duel.Winner = user2;

        _ratingManager.UpdateRatings(duel);

        user1.Rating.Should().Be(1480);
        user2.Rating.Should().Be(1520);
    }

    [Fact]
    public void GetTaskLevel_Returns_mapped_level_for_matching_interval()
    {
        var ratingManager = new RatingManager(
            Options.Create(new DuelOptions
            {
                DefaultMaxDurationMinutes = 30,
                RatingToTaskLevelMapping =
                [
                    new RatingToTaskLevelMappingItem { Rating = "0-1000", Level = 1 },
                    new RatingToTaskLevelMappingItem { Rating = "1001-2000", Level = 3 }
                ]
            }));

        ratingManager.GetTaskLevel(1500).Should().Be(3);
        ratingManager.GetTaskLevel(1000).Should().Be(1);
    }

    [Fact]
    public void GetTaskLevel_Returns_default_when_no_mapping_matches()
    {
        var ratingManager = new RatingManager(
            Options.Create(new DuelOptions
            {
                DefaultMaxDurationMinutes = 30,
                RatingToTaskLevelMapping =
                [
                    new RatingToTaskLevelMappingItem { Rating = "0-999", Level = 2 }
                ]
            }));

        ratingManager.GetTaskLevel(1500).Should().Be(1);
    }

    [Fact]
    public void GetRatingChanges_Uses_correct_k_for_rating_boundaries()
    {
        var user1 = CreateUser(1, 1600);
        var user2 = CreateUser(2, 1600);
        var duel = CreateDuel(1, user1, user2);

        var changes = _ratingManager.GetRatingChanges(duel, 1600, 1600);

        changes[DuelResult.Win].Should().Be(16);
        changes[DuelResult.Draw].Should().Be(0);
        changes[DuelResult.Lose].Should().Be(-16);
    }
}
