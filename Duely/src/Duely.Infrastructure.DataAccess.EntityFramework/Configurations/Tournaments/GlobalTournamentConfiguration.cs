using Duely.Domain.Models.Tournaments.Entities.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments;

internal sealed class GlobalTournamentConfiguration : IEntityTypeConfiguration<GlobalTournament>
{
    public void Configure(EntityTypeBuilder<GlobalTournament> builder)
    {
    }
}
