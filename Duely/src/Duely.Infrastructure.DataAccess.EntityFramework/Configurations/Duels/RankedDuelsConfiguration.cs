using Duely.Domain.Models.Duels.Entities.DuelParticipants;
using Duely.Domain.Models.Duels.Entities.Duels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class RankedDuelsConfiguration : IEntityTypeConfiguration<RankedDuel>
{
    public void Configure(EntityTypeBuilder<RankedDuel> builder)
    {
    }
}

internal sealed class RankedDuelParticipantsConfiguration : IEntityTypeConfiguration<RankedDuelParticipant>
{
    public void Configure(EntityTypeBuilder<RankedDuelParticipant> builder)
    {
        builder.Property(p => p.InitialRating);
    }
}
