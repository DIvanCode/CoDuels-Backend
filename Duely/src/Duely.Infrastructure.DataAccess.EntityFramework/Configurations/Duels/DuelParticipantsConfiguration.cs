using Duely.Domain.Models.Duels.Entities;
using Duely.Domain.Models.Duels.Entities.DuelParticipants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class DuelParticipantsConfiguration : IEntityTypeConfiguration<DuelParticipant>
{
    private const string TableName = "DuelParticipants";
    private const string UserIdColumnName = "UserId";
    private const string DuelIdColumnName = "DuelId";
    
    public void Configure(EntityTypeBuilder<DuelParticipant> builder)
    {
        builder.ToTable(TableName);
        
        builder.HasKey(UserIdColumnName, DuelIdColumnName);

        builder.Property(p => p.Type)
            .HasConversion<string>();
        builder.HasDiscriminator(p => p.Type)
            .HasValue<RankedDuelParticipant>(DuelType.RankedDuel);
        
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(UserIdColumnName);
        
        builder.HasOne(p => p.Duel)
            .WithMany()
            .HasForeignKey(DuelIdColumnName);
    }
}
