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

        builder.HasOne(d => d.User1)
            .WithMany(u => u.DuelsAsUser1)
            .HasForeignKey("User1Id")
            .IsRequired();
        
        builder.Property(d => d.User1InitRating)
            .HasColumnName("User1InitRating")
            .HasColumnType("integer")
            .IsRequired();
        
        builder.Property(d => d.User1FinalRating)
            .HasColumnName("User1FinalRating")
            .HasColumnType("integer")
            .IsRequired(false);

        builder.HasOne(d => d.User2)
            .WithMany(u => u.DuelsAsUser2)
            .HasForeignKey("User2Id")
            .IsRequired();
        
        builder.Property(d => d.User2InitRating)
            .HasColumnName("User2InitRating")
            .HasColumnType("integer")
            .IsRequired();
        
        builder.Property(d => d.User2FinalRating)
            .HasColumnName("User2FinalRating")
            .HasColumnType("integer")
            .IsRequired(false);

        builder.HasOne(d => d.Winner)
            .WithMany()
            .IsRequired(false);

        builder.HasMany(d => d.Submissions)
            .WithOne(s => s.Duel)
            .IsRequired();
    }
}
