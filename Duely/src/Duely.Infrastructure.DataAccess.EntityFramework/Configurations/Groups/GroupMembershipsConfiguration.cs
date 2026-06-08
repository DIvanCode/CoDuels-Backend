using Duely.Domain.Models.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;

internal sealed class GroupMembershipsConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    private const string TableName = "GroupMemberships";
    
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable(TableName);

        // builder.HasKey(m => new { m.Group, m.User });

        builder.HasOne(m => m.Group)
            .WithMany();

        builder.HasOne(m => m.User)
            .WithMany();

        builder.Property(m => m.Role)
            .HasConversion<string>();

        builder.Property(m => m.IsConfirmed);
    }
}
