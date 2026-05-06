using Duely.Domain.Models.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class TournamentsConfiguration : IEntityTypeConfiguration<Tournament>
{
    private const string GroupIdShadowKey = "GroupId";
    private const string CreatedByIdShadowKey = "CreatedById";
    private const string DuelConfigurationIdShadowKey = "DuelConfigurationId";

    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Tournaments");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(t => t.Name)
            .HasColumnName("Name")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("Status")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.MatchmakingType)
            .HasColumnName("MatchmakingType")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp")
            .IsRequired();

        builder.HasOne(t => t.Group)
            .WithMany(g => g.Tournaments)
            .HasForeignKey(GroupIdShadowKey)
            .IsRequired();

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(CreatedByIdShadowKey)
            .IsRequired();

        builder.HasOne(t => t.DuelConfiguration)
            .WithMany()
            .HasForeignKey(DuelConfigurationIdShadowKey)
            .IsRequired(false);

        builder.HasDiscriminator(t => t.MatchmakingType)
            .HasValue<SingleEliminationBracketTournament>(TournamentMatchmakingType.SingleEliminationBracket);

        builder.UseTphMappingStrategy();
    }
}
