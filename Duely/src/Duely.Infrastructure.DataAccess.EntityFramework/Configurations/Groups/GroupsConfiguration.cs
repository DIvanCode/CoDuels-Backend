using Duely.Domain.Models.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;

internal sealed class GroupsConfiguration : IEntityTypeConfiguration<Group>
{
    private const string TableName = "Groups";
    private const string MembershipsFieldName = "_memberships";
    
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .HasConversion<GroupNameConverter>();

        builder.Navigation(g => g.Memberships)
            .HasField(MembershipsFieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class GroupNameConverter : ValueConverter<GroupName, string>
{
    public GroupNameConverter()
        : base(groupName => groupName.Value, value => new GroupName(value))
    {
    }
}
