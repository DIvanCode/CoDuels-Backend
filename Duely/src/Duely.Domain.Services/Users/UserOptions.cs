namespace Duely.Domain.Services.Users;

public sealed class UserOptions
{
    public const string SectionName = "Users";

    public int InitialRating { get; init; } = 1500;
}
