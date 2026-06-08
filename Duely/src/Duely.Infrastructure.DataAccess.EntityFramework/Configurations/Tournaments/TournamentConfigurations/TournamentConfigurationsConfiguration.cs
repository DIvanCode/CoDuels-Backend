using Duely.Domain.Models.Tournaments.Entities;
using Duely.Domain.Models.Tournaments.Entities.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments.TournamentConfigurations;

internal sealed class TournamentConfigurationsConfiguration : IEntityTypeConfiguration<TournamentConfiguration>
{
    private const string TableName = "TournamentConfigurations";
    // internal const string IdColumnName = "Id";
    
    public void Configure(EntityTypeBuilder<TournamentConfiguration> builder)
    {
        builder.ToTable(TableName);
        
        // builder.HasKey(IdColumnName);
        //
        // builder.Property(IdColumnName)
        //     .HasColumnType("integer")
        //     .HasColumnName(IdColumnName)
        //     .ValueGeneratedOnAdd();
        
        builder.Property(c => c.Type)
            .HasConversion<string>();
        builder.HasDiscriminator(c => c.Type)
            .HasValue<GroupStageTournamentConfiguration>(TournamentConfigurationType.GroupStage)
            .HasValue<SingleEliminationBracketTournamentConfiguration>(TournamentConfigurationType.SingleEliminationBracket);

        builder.HasOne(d => d.DuelConfiguration)
            .WithMany();
    }
}
