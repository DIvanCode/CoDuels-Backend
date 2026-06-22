using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.IntegrationEvents;

internal sealed class IntegrationEventsConfiguration : IEntityTypeConfiguration<IntegrationEvent>
{
    private const string TableName = "IntegrationEvents";
    
    public void Configure(EntityTypeBuilder<IntegrationEvent> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
        
        builder.Property(e => e.Type)
            .HasConversion<string>();
        builder.HasDiscriminator(e => e.Type)
            .HasValue<UserCreatedIntegrationEvent>(IntegrationEventType.UserCreated);
        
        builder.Property(e => e.CreatedAt);

        builder.Property(e => e.Status)
            .HasConversion<string>();

        builder.Property(e => e.ProcessAttempts);

        builder.Property(e => e.NextProcessAttemptAt);

        builder.HasIndex(u => u.Status);
    }
}
