using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments.TournamentConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments;

internal sealed class TournamentsConfiguration : IEntityTypeConfiguration<Tournament>
{
    private const string TableName = "Tournaments";
    private const string CreatedByIdColumnName = "CreatedById";
    
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName(nameof(Tournament.Id))
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasColumnName(nameof(Tournament.Name))
            .HasConversion<TournamentNameConverter>();

        builder.Property(t => t.Type)
            .HasColumnName(nameof(Tournament.Type))
            .HasConversion<string>();

        builder.HasDiscriminator(t => t.Type)
            .HasValue<GlobalTournament>(TournamentType.Global)
            .HasValue<GroupTournament>(TournamentType.Group);
        
        builder.Property(t => t.Status)
            .HasColumnName(nameof(Tournament.Status))
            .HasConversion<string>();

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(CreatedByIdColumnName);
        
        builder.Property(t => t.CreatedAt).HasColumnName(nameof(Tournament.CreatedAt));

        builder.HasOne(t => t.Configuration)
            .WithOne()
            .HasForeignKey(TournamentConfigurationsConfiguration.IdColumnName);

        builder.HasMany(t => t.Participants)
            .WithOne();
    }
}

internal sealed class TournamentNameConverter : ValueConverter<TournamentName, string>
{
    public TournamentNameConverter()
        : base(groupName => groupName.Value, value => new TournamentName(value))
    {
    }
}
