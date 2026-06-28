using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class DuelsConfiguration : IEntityTypeConfiguration<Duel>
{
    private const string TableName = "Duels";
    private const string ParticipantsFieldName = "_participants";
    private const string ProblemsFieldName = "_problems";
    
    public void Configure(EntityTypeBuilder<Duel> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd();

        builder.Property(d => d.Type)
            .HasConversion<string>();
        builder.HasDiscriminator(d => d.Type)
            .HasValue<RankedDuel>(DuelType.Ranked);

        builder.HasOne(d => d.Configuration)
            .WithMany();
        
        builder.Navigation(d => d.Participants)
            .HasField(ParticipantsFieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(d => d.Problems)
            .HasField(ProblemsFieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(d => d.Status)
            .HasConversion<string>();

        builder.Property(d => d.CreatedAt);
        
        builder.Property(d => d.UpdatedAt)
            .IsConcurrencyToken();

        builder.Property(d => d.StartedAt);
    }
}
