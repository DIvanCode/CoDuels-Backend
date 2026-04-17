using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class AnticheatScoresConfiguration : IEntityTypeConfiguration<AnticheatScore>
{
    private const string DuelIdShadowKey = "DuelId";
    private const string UserIdShadowKey = "UserId";

    public void Configure(EntityTypeBuilder<AnticheatScore> builder)
    {
        builder.ToTable("AnticheatScores");

        builder.HasKey(DuelIdShadowKey, UserIdShadowKey, nameof(AnticheatScore.TaskKey));

        builder.Property<int>(DuelIdShadowKey)
            .HasColumnName("DuelId")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property<int>(UserIdShadowKey)
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

        builder.HasOne(score => score.Duel)
            .WithMany()
            .HasForeignKey(DuelIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(score => score.User)
            .WithMany()
            .HasForeignKey(UserIdShadowKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
