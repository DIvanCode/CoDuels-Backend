using Duely.Domain.Models.Duels.Pending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class TournamentPendingDuelsConfiguration : IEntityTypeConfiguration<TournamentPendingDuel>
{
    private const string TournamentIdShadowKey = "TournamentId";
    private const string User1IdShadowKey = "User1Id";
    private const string User2IdShadowKey = "User2Id";
    private const string ConfigurationIdShadowKey = "ConfigurationId";

    public void Configure(EntityTypeBuilder<TournamentPendingDuel> builder)
    {
        builder.HasOne(d => d.Tournament)
            .WithMany()
            .HasForeignKey(TournamentIdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.User1)
            .WithMany()
            .HasForeignKey(User1IdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.User2)
            .WithMany()
            .HasForeignKey(User2IdShadowKey)
            .IsRequired();

        builder.HasOne(d => d.Configuration)
            .WithMany()
            .HasForeignKey(ConfigurationIdShadowKey)
            .IsRequired(false);
    }
}
