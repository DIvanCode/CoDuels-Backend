using Duely.Domain.Models.Duels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class ProblemsConfiguration : IEntityTypeConfiguration<Problem>
{
    private const string TableName = "Problems";
    
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.ExternalSystemName);

        builder.Property(p => p.ExternalId);

        builder.Property(p => p.Title);
        
        builder.HasIndex(p => new { p.ExternalSystemName , p.ExternalId }).IsUnique();
    }
}
