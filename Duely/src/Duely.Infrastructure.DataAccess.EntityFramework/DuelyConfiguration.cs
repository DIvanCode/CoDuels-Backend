using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class DuelyConfiguration : IEntityTypeConfiguration<Duel>
{
    public void Configure(EntityTypeBuilder<Duel> builder)
    {
        builder.ToTable("Duel");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(d => d.TaskId)
            .HasColumnName("TaskId")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.User1Id)
            .HasColumnName("User1Id")
            .HasColumnType("integer");

        builder.Property(d => d.User2Id)
            .HasColumnName("User2Id")
            .HasColumnType("integer");

        builder.Property(d => d.Status)
            .HasColumnName("Status")
            .HasConversion<string>()
            .HasColumnType("text");

        builder.Property(d => d.Result)
            .HasColumnName("Result")
            .HasConversion<string>()
            .HasColumnType("text");

        builder.Property(d => d.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("timestamp");

        builder.Property(d => d.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("timestamp");

        builder.Property(d => d.MaxDuration)
            .HasColumnName("MaxDuration")
            .HasColumnType("integer")
            .HasDefaultValue(30);

    }
}
