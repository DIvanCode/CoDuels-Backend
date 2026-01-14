using Duely.Domain.Services.Duels;
using FluentAssertions;
using Xunit;

namespace Duely.Domain.Tests;

public class RatingToTaskLevelMappingItemTests
{
    [Fact]
    public void GetInterval_Parses_min_and_max()
    {
        var item = new RatingToTaskLevelMappingItem
        {
            Rating = "1200-1800",
            Level = 2
        };

        var interval = item.GetInterval();

        interval.MinRating.Should().Be(1200);
        interval.MaxRating.Should().Be(1800);
    }
}
