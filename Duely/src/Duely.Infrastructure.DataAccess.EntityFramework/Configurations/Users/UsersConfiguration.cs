using Duely.Domain.Models.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Users;

internal sealed class UsersConfiguration : IEntityTypeConfiguration<User>
{
    private const string TableName = "Users";
    private const string DuelsParticipationFieldName = "_duelsParticipation";
    
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();
        
        builder.Property(u => u.CreatedAt);

        builder.Property(u => u.Nickname);

        builder.Property(u => u.PasswordHash);
        
        builder.Property(u => u.PasswordSalt);
        
        builder.Property(u => u.IsAdmin);

        builder.Property(u => u.RefreshToken);

        builder.Property(u => u.IdentityTicket);

        builder.Property(u => u.Rating);

        builder.Navigation(u => u.DuelsParticipation)
            .HasField(DuelsParticipationFieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(u => u.Nickname).IsUnique();
        // also create unique index on u.Nickname.ToLower() in migration
        builder.HasIndex(u => u.RefreshToken).IsUnique();
        builder.HasIndex(u => u.IdentityTicket).IsUnique();
    }
}
