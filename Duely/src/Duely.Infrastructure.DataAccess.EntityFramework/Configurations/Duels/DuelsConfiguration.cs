using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class DuelsConfiguration : IEntityTypeConfiguration<Duel>
{
    private const string TableName = "Duels";
    
    public void Configure(EntityTypeBuilder<Duel> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd();

        builder.Property(d => d.Type)
            .HasConversion<string>();
        builder.HasDiscriminator(d => d.Type)
            .HasValue<RankedDuel>(DuelType.RankedDuel);

        builder.HasOne(d => d.Configuration)
            .WithMany();

        builder.HasMany(d => d.Problems)
            .WithOne(p => p.Duel);

        builder.Property(d => d.Status)
            .HasConversion<string>();

        builder.Property(d => d.CreatedAt);
    }
}
