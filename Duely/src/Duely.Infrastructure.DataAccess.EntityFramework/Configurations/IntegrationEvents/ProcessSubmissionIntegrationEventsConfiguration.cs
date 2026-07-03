using Duely.Domain.Models.Duels.Entities;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.IntegrationEvents;

internal sealed class ProcessSubmissionIntegrationEventsConfiguration
    : IEntityTypeConfiguration<ProcessSubmissionIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessSubmissionIntegrationEvent> builder)
    {
        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(e => e.SubmissionId);
    }
}
