using Duely.Domain.Models.Tournaments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments.TournamentParticipants;

public sealed class TournamentParticipantsConfiguration : IEntityTypeConfiguration<TournamentParticipant>
{
    private const string TableName = "TournamentParticipants";
    private const string TournamentIdColumnName = "TournamentId";
    private const string UserIdColumnName = "UserId";

    public void Configure(EntityTypeBuilder<TournamentParticipant> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(TournamentIdColumnName, UserIdColumnName);

        builder.HasOne(p => p.Tournament)
            .WithMany()
            .HasForeignKey(TournamentIdColumnName);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(UserIdColumnName);

        builder.Property(p => p.Seed).HasColumnName(nameof(TournamentParticipant.Seed));
    }
}
