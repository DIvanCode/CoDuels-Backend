using Duely.Domain.Models.Groups.Entities;
using Duely.Domain.Models.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;

public sealed class GroupsConfiguration : IEntityTypeConfiguration<Group>
{
    private const string TableName = "Groups";
    
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName(nameof(Group.Id))
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .HasColumnName(nameof(Group.Name))
            .HasConversion<GroupNameConverter>();

        builder.HasMany(g => g.Memberships)
            .WithOne(g => g.Group);
    }
}

internal sealed class GroupNameConverter : ValueConverter<GroupName, string>
{
    public GroupNameConverter()
        : base(groupName => groupName.Value, value => new GroupName(value))
    {
    }
}
