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
    private const string NicknameColumnName = "Nickname";
    private const string NicknameLowerColumnName = "NicknameLower";
    
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName(nameof(User.Id))
            .ValueGeneratedNever();

        builder.Property(u => u.Nickname.Value).HasColumnName(NicknameColumnName);
        
        builder.Property(u => u.Nickname.LowerValue).HasColumnName(NicknameLowerColumnName);

        builder.Property(u => u.Password)
            .HasColumnName(nameof(User.Password))
            .HasConversion(
                from => JsonSerializer.Serialize(from, PasswordJsonContext.Default.Password),
                to => JsonSerializer.Deserialize<Password>(to, PasswordJsonContext.Default.Password)!);
        
        builder.Property(u => u.CreatedAt).HasColumnName(nameof(User.CreatedAt));
        
        builder.Property(u => u.IsAdmin).HasColumnName(nameof(User.IsAdmin));

        builder.Property(u => u.RefreshToken).HasColumnName(nameof(User.RefreshToken));

        builder.Property(u => u.IdentityTicket).HasColumnName(nameof(User.IdentityTicket));

        builder.Property(s => s.Rating)
            .HasColumnName(nameof(User.Rating))
            .HasConversion<RatingConverter>();

        builder.HasIndex(u => u.Nickname.LowerValue);
        
        builder.HasIndex(u => u.RefreshToken);
        
        builder.HasIndex(u => u.IdentityTicket);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(Password))]
internal sealed partial class PasswordJsonContext : JsonSerializerContext;

internal sealed class RatingConverter : ValueConverter<Rating, int>
{
    public RatingConverter()
        : base(rating => rating.Value, value => new Rating(value))
    {
    }
}
