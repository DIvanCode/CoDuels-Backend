using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class AnticheatScoresConfiguration : IEntityTypeConfiguration<AnticheatScore>
{
    public void Configure(EntityTypeBuilder<AnticheatScore> builder)
    {
        builder.ToTable("AnticheatScores");

        builder.HasKey(score => new { score.DuelId, score.UserId, score.TaskKey });

        builder.Property(score => score.DuelId)
            .HasColumnName("DuelId")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(score => score.UserId)
            .HasColumnName("UserId")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(score => score.TaskKey)
            .HasColumnName("TaskKey")
            .HasColumnType("varchar(1)")
            .IsRequired();

        builder.Property(score => score.Score)
            .HasColumnName("Score")
            .HasColumnType("real")
            .IsRequired(false);

        builder.HasOne<Duel>()
            .WithMany()
            .HasForeignKey(score => score.DuelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
