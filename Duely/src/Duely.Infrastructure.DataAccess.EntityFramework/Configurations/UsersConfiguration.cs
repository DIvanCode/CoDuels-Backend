using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class UsersConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(u => u.Nickname)
            .HasColumnName("Nickname")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(u => u.PasswordSalt)
            .HasColumnName("PasswordSalt")
            .HasColumnType("text")
            .IsRequired();
        
        builder.Property(u => u.RefreshToken)
            .HasColumnName("RefreshToken")
            .HasColumnType("text")
            .IsRequired(false);
    }
}