using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class GroupPendingDuelsConfiguration : IEntityTypeConfiguration<GroupPendingDuel>
{
    private const string CreatedByUserIdShadowKey = "CreatedById";
    private const string User1IdShadowKey = "User1Id";
    private const string User2IdShadowKey = "User2Id";
    private const string GroupIdShadowKey = "GroupId";
    private const string ConfigurationIdShadowKey = "ConfigurationId";

    public void Configure(EntityTypeBuilder<GroupPendingDuel> builder)
    {
        builder.HasOne(d => d.Group)
            .WithMany()
            .HasForeignKey(GroupIdShadowKey)
            .IsRequired();
        
        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(CreatedByUserIdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.User1)
            .WithMany()
            .HasForeignKey(User1IdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.User2)
            .WithMany()
            .HasForeignKey(User2IdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.Configuration)
            .WithMany()
            .HasForeignKey(ConfigurationIdShadowKey)
            .IsRequired(false);
        
        builder.Property(d => d.IsAcceptedByUser1)
            .HasColumnName("IsAcceptedByUser1")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(d => d.IsAcceptedByUser2)
            .HasColumnName("IsAcceptedByUser2")
            .HasColumnType("boolean")
            .IsRequired();
    }
}
