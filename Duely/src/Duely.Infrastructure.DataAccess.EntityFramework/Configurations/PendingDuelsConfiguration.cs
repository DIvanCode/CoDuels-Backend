using Duely.Domain.Models.Duels.Pending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class PendingDuelsConfiguration : IEntityTypeConfiguration<PendingDuel>
{
    public void Configure(EntityTypeBuilder<PendingDuel> builder)
    {
        builder.ToTable("PendingDuels");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.HasDiscriminator(d => d.Type)
            .HasValue<RankedPendingDuel>(PendingDuelType.Ranked)
            .HasValue<FriendlyPendingDuel>(PendingDuelType.Friendly)
            .HasValue<GroupPendingDuel>(PendingDuelType.Group);

        builder.Property(d => d.Type)
            .HasColumnName("Type")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.UseTphMappingStrategy();
    }
}
