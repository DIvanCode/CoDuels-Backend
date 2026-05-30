using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.FriendlyDuels;
using Duely.Domain.Models.Duels.GroupDuels;
using Duely.Domain.Models.Duels.RankedDuels;
using Duely.Domain.Models.Duels.TournamentDuels;
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
            .HasValue<GroupPendingDuel>(PendingDuelType.Group)
            .HasValue<TournamentPendingDuel>(PendingDuelType.Tournament);

        builder.Property(d => d.Type)
            .HasColumnName("Type")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp")
            .IsRequired();

        builder.UseTphMappingStrategy();
    }
}
