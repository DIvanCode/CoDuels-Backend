// using Duely.Domain.Models.Duels.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class SolutionsConfiguration : IEntityTypeConfiguration<Solution>
// {
//     private const string TableName = "Solutions";
//     private const string UserIdColumnName = "UserId";
//     private const string ProblemIdColumnName = "DuelProblemId";
//     
//     public void Configure(EntityTypeBuilder<Solution> builder)
//     {
//         builder.ToTable(TableName);
//         
//         builder.HasKey(s => s.Id);
//         builder.Property(s => s.Id)
//             .ValueGeneratedOnAdd();
//
//         builder.HasOne(s => s.User)
//             .WithMany()
//             .HasForeignKey(UserIdColumnName);
//
//         builder.HasOne(s => s.Problem)
//             .WithMany()
//             .HasForeignKey(ProblemIdColumnName);
//
//         builder.Property(s => s.Source);
//
//         builder.Property(s => s.Language)
//             .HasConversion<string>();
//         
//         builder.HasIndex(UserIdColumnName, ProblemIdColumnName);
//     }
// }
