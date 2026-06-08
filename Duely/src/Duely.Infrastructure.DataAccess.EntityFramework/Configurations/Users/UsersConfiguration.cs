using System.Text.Json;
using System.Text.Json.Serialization;
using Duely.Domain.Models.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Users;

internal sealed class UsersConfiguration : IEntityTypeConfiguration<User>
{
    private const string TableName = "Users";
    private const string NicknameValueColumnName = "Nickname";
    private const string NicknameLowerValueColumnName = "NicknameLower";
    
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.Nickname.Value).HasColumnName(NicknameValueColumnName);
        builder.Property(u => u.Nickname.LowerValue).HasColumnName(NicknameLowerValueColumnName);

        builder.Property(u => u.Password)
            .HasConversion<PasswordConverter>();

        builder.Property(u => u.CreatedAt);
        
        builder.Property(u => u.IsAdmin);

        builder.Property(u => u.RefreshToken);

        builder.Property(u => u.IdentityTicket);

        builder.Property(s => s.Rating)
            .HasConversion<RatingConverter>();

        builder.HasIndex(u => u.Nickname.LowerValue);
        builder.HasIndex(u => u.RefreshToken);
        builder.HasIndex(u => u.IdentityTicket);
    }
}

internal sealed class PasswordConverter : ValueConverter<Password, string>
{
    private static readonly JsonSerializerOptions Options = new();
    
    public PasswordConverter()
        : base(
            from => JsonSerializer.Serialize(from, Options),
            to => JsonSerializer.Deserialize<Password>(to, Options)!)
    {
    }
}

internal sealed class RatingConverter : ValueConverter<Rating, int>
{
    public RatingConverter()
        : base(rating => rating.Value, value => new Rating(value))
    {
    }
}
