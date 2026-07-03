using Duely.Domain.Models.Duels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    private const string TableName = "Submissions";
    private const string UserIdColumnName = "UserId";
    private const string DuelIdColumnName = "DuelId";
    private const string ProblemIdColumnName = "ProblemId";
    
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable(TableName);
        
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();
        
        builder.HasOne(s => s.User)
             .WithMany()
             .HasForeignKey(UserIdColumnName);

         builder.HasOne(s => s.Problem)
             .WithMany()
             .HasForeignKey(DuelIdColumnName, ProblemIdColumnName);

        builder.Property(s => s.Source);

        builder.Property(s => s.Language)
            .HasConversion<string>();

        builder.Property(s => s.Status)
            .HasConversion<string>();

        builder.Property(s => s.CreatedAt);
        
        builder.HasIndex(UserIdColumnName, DuelIdColumnName, ProblemIdColumnName);
        builder.HasIndex(DuelIdColumnName, UserIdColumnName, ProblemIdColumnName);
    }
}
