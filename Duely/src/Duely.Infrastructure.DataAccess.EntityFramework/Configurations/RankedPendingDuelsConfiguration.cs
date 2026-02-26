using Duely.Domain.Models.Duels.Pending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class RankedPendingDuelsConfiguration : IEntityTypeConfiguration<RankedPendingDuel>
{
    private const string UserIdShadowKey = "UserId";

    public void Configure(EntityTypeBuilder<RankedPendingDuel> builder)
    {
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(UserIdShadowKey)
            .IsRequired();
        
        builder.Property(d => d.Rating)
            .HasColumnName("Rating")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(d => d.EnqueuedAt)
            .HasColumnName("EnqueuedAt")
            .HasColumnType("timestamp")
            .IsRequired();
    }
}
