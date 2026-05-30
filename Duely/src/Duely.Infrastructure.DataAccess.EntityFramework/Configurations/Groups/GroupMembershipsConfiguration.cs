using Duely.Domain.Models.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;

public sealed class GroupMembershipsConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    private const string TableName = "GroupMemberships";
    private const string GroupIdColumnName = "GroupId";
    private const string UserIdColumnName = "UserId";
    
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName(nameof(GroupMembership.Id))
            .ValueGeneratedNever();
        
        builder.HasOne(m => m.Group)
            .WithMany(g => g.Memberships)
            .HasForeignKey(GroupIdColumnName)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(UserIdColumnName)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.Role)
            .HasColumnName(nameof(GroupMembership.Role))
            .HasConversion<string>();

        builder.Property(m => m.IsConfirmed).HasColumnName(nameof(GroupMembership.IsConfirmed));
        
        builder.HasIndex(UserIdColumnName, GroupIdColumnName).IsUnique();
        
        builder.HasIndex(GroupIdColumnName, UserIdColumnName).IsUnique();
    }
}
