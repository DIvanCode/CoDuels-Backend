using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class UserGroupRolesConfiguration : IEntityTypeConfiguration<UserGroupRole>
{
    private const string UserIdShadowKey = "UserId";
    private const string GroupIdShadowKey = "GroupId";
    
    public void Configure(EntityTypeBuilder<UserGroupRole> builder)
    {
        builder.ToTable("UserGroupRoles");

        builder.HasKey(UserIdShadowKey, GroupIdShadowKey);

        builder.Property<int>("UserId")
            .HasColumnName("UserId")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property<int>("GroupId")
            .HasColumnName("GroupId")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(ug => ug.Role)
            .HasColumnName("Role")
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        builder.HasOne(ug => ug.User)
            .WithMany(u => u.Groups)
            .HasForeignKey(UserIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ug => ug.Group)
            .WithMany(g => g.Users)
            .HasForeignKey(GroupIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
