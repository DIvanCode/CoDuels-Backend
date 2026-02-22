using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class GroupMembershipsConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    private const string UserIdShadowKey = "UserId";
    private const string GroupIdShadowKey = "GroupId";
    private const string InvitedByIdShadowKey = "InvitedById";
    
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("GroupMemberships");

        builder.HasOne(m => m.User)
            .WithMany(u => u.Groups)
            .HasForeignKey(UserIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Users)
            .HasForeignKey(GroupIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.Role)
            .HasColumnName("Role")
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.InvitationPending)
            .HasColumnName("InvitationPending")
            .HasColumnType("boolean")
            .IsRequired();

        builder.HasOne(m => m.InvitedBy)
            .WithMany()
            .HasForeignKey(InvitedByIdShadowKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasKey(UserIdShadowKey, GroupIdShadowKey);
    }
}
