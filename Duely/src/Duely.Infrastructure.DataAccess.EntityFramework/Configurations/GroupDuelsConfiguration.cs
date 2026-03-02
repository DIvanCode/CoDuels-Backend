using Duely.Domain.Models.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class GroupDuelsConfiguration : IEntityTypeConfiguration<GroupDuel>
{
    private const string GroupIdShadowKey = "GroupId";
    private const string DuelIdShadowKey = "DuelId";
    private const string CreatedByIdShadowKey = "CreatedById";
    
    public void Configure(EntityTypeBuilder<GroupDuel> builder)
    {
        builder.ToTable("GroupDuels");

        builder.HasKey(GroupIdShadowKey, DuelIdShadowKey);

        builder.HasOne(d => d.Group)
            .WithMany(g => g.Duels)
            .HasForeignKey(GroupIdShadowKey);

        builder.HasOne(d => d.Duel)
            .WithMany()
            .HasForeignKey(DuelIdShadowKey);

        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(CreatedByIdShadowKey);
    }
}
