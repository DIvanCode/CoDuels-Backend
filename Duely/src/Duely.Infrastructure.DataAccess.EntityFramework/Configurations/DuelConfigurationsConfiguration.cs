using System.Text.Json;
using Duely.Domain.Models.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class DuelConfigurationsConfiguration : IEntityTypeConfiguration<DuelConfiguration>
{
    private const string TableName = nameof(DuelConfiguration) + "s";
    private const string OwnerId = "OwnerId";
    
    public void Configure(EntityTypeBuilder<DuelConfiguration> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName(nameof(DuelConfiguration.Id))
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(OwnerId)
            .IsRequired(false);
        
        builder.Property(c => c.IsDeleted)
            .HasColumnName(nameof(DuelConfiguration.IsDeleted))
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.IsRated)
            .HasColumnName(nameof(DuelConfiguration.IsRated))
            .HasColumnType("boolean")
            .IsRequired();
        
        builder.Property(c => c.ShouldShowOpponentSolution)
            .HasColumnName(nameof(DuelConfiguration.ShouldShowOpponentSolution))
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(c => c.MaxDurationMinutes)
            .HasColumnName(nameof(DuelConfiguration.MaxDurationMinutes))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.TasksCount)
            .HasColumnName(nameof(DuelConfiguration.TasksCount))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.TasksOrder)
            .HasColumnName(nameof(DuelConfiguration.TasksOrder))
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();
    }
}
