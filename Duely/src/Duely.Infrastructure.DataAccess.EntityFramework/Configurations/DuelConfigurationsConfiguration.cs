using System.Text.Json;
using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class DuelConfigurationsConfiguration : IEntityTypeConfiguration<DuelConfiguration>
{
    public void Configure(EntityTypeBuilder<DuelConfiguration> builder)
    {
        builder.ToTable("DuelConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(c => c.IsRated)
            .HasColumnName("IsRated")
            .HasColumnType("boolean")
            .IsRequired();
        
        builder.Property(c => c.ShouldShowOpponentCode)
            .HasColumnName("ShouldShowOpponentCode")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(c => c.MaxDurationMinutes)
            .HasColumnName("MaxDurationMinutes")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.TasksCount)
            .HasColumnName("TasksCount")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.TasksOrder)
            .HasColumnName("TasksOrder")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();
        
        builder.Property(c => c.TasksConfigurations)
            .HasColumnName("TasksConfigurations")
            .HasConversion(
                obj => JsonSerializer.Serialize(obj, new JsonSerializerOptions()),
                str => JsonSerializer.Deserialize<Dictionary<char, DuelTaskConfiguration>>(str, new JsonSerializerOptions())!);
    }
}

