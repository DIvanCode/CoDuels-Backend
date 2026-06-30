using Duely.Domain.Models.Duels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class DuelConfigurationsConfiguration : IEntityTypeConfiguration<DuelConfiguration>
{
    private const string TableName = "DuelConfigurations";
    
    public void Configure(EntityTypeBuilder<DuelConfiguration> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.IsRated);
        
        builder.Property(c => c.ShowOpponentSolution);
        
        builder.Property(c => c.DurationMinutes);
        
        builder.Property(c => c.ProblemsCount);
        
        builder.Property(c => c.ProblemsOrder)
            .HasConversion<string>();

        builder.HasOne(c => c.CreatedBy)
            .WithMany();
    }
}
