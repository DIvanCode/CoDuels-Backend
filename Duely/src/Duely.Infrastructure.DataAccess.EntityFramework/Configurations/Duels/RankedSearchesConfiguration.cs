// using Duely.Domain.Models.Duels.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class RankedSearchesConfiguration : IEntityTypeConfiguration<RankedSearch>
// {
//     private const string TableName = "RankedSearches";
//     
//     public void Configure(EntityTypeBuilder<RankedSearch> builder)
//     {
//         builder.ToTable(TableName);
//
//         builder.HasKey(s => s.Id);
//
//         builder.Property(s => s.Id)
//             .ValueGeneratedNever();
//
//         builder.HasOne(s => s.User)
//             .WithMany();
//
//         builder.Property(s => s.StartedAt);
//
//         builder.Property(s => s.Seed);
//     }
// }
