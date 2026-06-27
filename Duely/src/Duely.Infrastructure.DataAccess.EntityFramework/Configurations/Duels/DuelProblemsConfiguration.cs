using Duely.Domain.Models.Duels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class DuelProblemsConfiguration : IEntityTypeConfiguration<DuelProblem>
{
    private const string TableName = "DuelProblems";
    public const string DuelIdColumnName = "DuelId";
    private const string ProblemIdColumnName = "ProblemId";
    
    public void Configure(EntityTypeBuilder<DuelProblem> builder)
    {
        builder.ToTable(TableName);
        
        builder.HasKey(DuelIdColumnName, ProblemIdColumnName);
        
        builder.HasOne(p => p.Duel)
            .WithMany(d => d.Problems)
            .HasForeignKey(DuelIdColumnName);
        
        builder.HasOne(p => p.Problem)
            .WithMany()
            .HasForeignKey(ProblemIdColumnName);

        builder.Property(p => p.Position);

        builder.Property(p => p.IsVisible);
    }
}
