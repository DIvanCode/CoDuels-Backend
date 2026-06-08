// using Duely.Domain.Models.Duels.Entities.Duels;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class FriendlyDuelsConfiguration : IEntityTypeConfiguration<FriendlyDuel>
// {
//     public void Configure(EntityTypeBuilder<FriendlyDuel> builder)
//     {
//         builder.HasOne(d => d.CreatedBy)
//             .WithMany();
//         
//         builder.Property(d => d.IsConfirmed);
//     }
// }
