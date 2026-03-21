using Duely.Domain.Models.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class TournamentParticipantsConfiguration : IEntityTypeConfiguration<TournamentParticipant>
{
    private const string TournamentIdShadowKey = "TournamentId";
    private const string UserIdShadowKey = "UserId";

    public void Configure(EntityTypeBuilder<TournamentParticipant> builder)
    {
        builder.ToTable("TournamentParticipants");

        builder.HasKey(TournamentIdShadowKey, UserIdShadowKey);

        builder.HasOne(p => p.Tournament)
            .WithMany(t => t.Participants)
            .HasForeignKey(TournamentIdShadowKey)
            .IsRequired();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(UserIdShadowKey)
            .IsRequired();

        builder.Property(p => p.Seed)
            .HasColumnName("Seed")
            .HasColumnType("integer")
            .IsRequired();
    }
}
