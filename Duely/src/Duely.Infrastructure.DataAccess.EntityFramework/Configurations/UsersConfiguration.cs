using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class UsersConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(s => s.Nickname)
            .HasColumnName("Nickname")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.PasswordSalt)
            .HasColumnName("PasswordSalt")
            .HasColumnType("text")
            .IsRequired();
        
        builder.Property(s => s.RefreshToken)
            .HasColumnName("RefreshToken")
            .HasColumnType("text")
            .IsRequired(false);
    }
}