using Duely.Domain.Models.Users.Entities;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.IntegrationEvents;

internal sealed class UserCreatedIntegrationEventsConfiguration : IEntityTypeConfiguration<UserCreatedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<UserCreatedIntegrationEvent> builder)
    {
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId);
    }
}
