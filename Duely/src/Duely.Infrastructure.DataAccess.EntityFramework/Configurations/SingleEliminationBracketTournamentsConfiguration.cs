using System.Text.Json;
using Duely.Domain.Models.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class SingleEliminationBracketTournamentsConfiguration
    : IEntityTypeConfiguration<SingleEliminationBracketTournament>
{
    public void Configure(EntityTypeBuilder<SingleEliminationBracketTournament> builder)
    {
        builder.Property(t => t.Nodes)
            .HasColumnName("Nodes")
            .HasColumnType("text")
            .HasConversion(
                obj => JsonSerializer.Serialize(obj, new JsonSerializerOptions()),
                str => JsonSerializer.Deserialize<List<SingleEliminationBracketNode?>>(str, new JsonSerializerOptions())!);
    }
}
