using Duely.Domain.Models.Duels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class RankedDuelSearchersConfiguration : IEntityTypeConfiguration<RankedDuelSearcher>
{
    private const string TableName = "RankedDuelSearchers";
    private const string UserIdColumnName = "UserId";
    
    public void Configure(EntityTypeBuilder<RankedDuelSearcher> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(UserIdColumnName);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(UserIdColumnName);

        builder.Property(s => s.CreatedAt);
    }
}
