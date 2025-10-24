using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class DuelyConfiguration : IEntityTypeConfiguration<Duel>
{
    public void Configure(EntityTypeBuilder<Duel> builder)
    {
        builder.ToTable("Duels");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(d => d.TaskId)
            .HasColumnName("TaskId")
            .HasColumnType("text")
            .IsRequired();

        builder.HasOne(d => d.User1)
            .WithMany()
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.User2)
            .WithMany()
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(d => d.Status)
            .HasColumnName("Status")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(d => d.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("timestamp")
            .IsRequired();
        
        builder.Property(d => d.DeadlineTime)
            .HasColumnName("DeadlineTime")
            .HasColumnType("timestamp")
            .IsRequired();

        builder.Property(d => d.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("timestamp")
            .IsRequired(false);

        builder.HasOne(d => d.Winner)
            .WithMany()
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(d => d.Submissions)
            .WithOne(s => s.Duel)
            .IsRequired();
    }
}
