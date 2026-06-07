using Duely.Domain.Models.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;

internal sealed class GroupMembershipsConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    private const string TableName = "GroupMemberships";
    private const string GroupIdColumnName = "GroupId";
    private const string UserIdColumnName = "UserId";
    
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(GroupIdColumnName, UserIdColumnName);
        
        builder.HasOne(m => m.Group)
            .WithMany()
            .HasForeignKey(GroupIdColumnName);
        
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(UserIdColumnName);

        builder.Property(m => m.Role)
            .HasColumnName(nameof(GroupMembership.Role))
            .HasConversion<string>();

        builder.Property(m => m.IsConfirmed).HasColumnName(nameof(GroupMembership.IsConfirmed));
    }
}
