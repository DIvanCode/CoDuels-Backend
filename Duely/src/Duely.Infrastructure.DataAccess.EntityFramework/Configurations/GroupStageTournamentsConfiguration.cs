using System.Text.Json;
using Duely.Domain.Models.Tournaments;
using Duely.Domain.Models.Tournaments.GroupStageTournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class GroupStageTournamentsConfiguration : IEntityTypeConfiguration<GroupStageTournament>
{
    public void Configure(EntityTypeBuilder<GroupStageTournament> builder)
    {
        builder.Property(t => t.DuelIds)
            .HasColumnName("GroupStageDuelIds")
            .HasColumnType("text")
            .HasConversion(
                obj => JsonSerializer.Serialize(obj, new JsonSerializerOptions()),
                str => JsonSerializer.Deserialize<List<int>>(str, new JsonSerializerOptions())!);
    }
}
