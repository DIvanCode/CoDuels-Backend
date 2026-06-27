using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.IntegrationEvents;

internal sealed class StartDuelIntegrationEventsConfiguration
    : IEntityTypeConfiguration<StartDuelIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<StartDuelIntegrationEvent> builder)
    {
        builder.HasOne<Duel>()
            .WithMany()
            .HasForeignKey(e => e.DuelId);
    }
}
