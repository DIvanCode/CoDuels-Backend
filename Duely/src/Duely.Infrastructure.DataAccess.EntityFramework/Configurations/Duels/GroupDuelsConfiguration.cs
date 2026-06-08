// using Duely.Domain.Models.Duels.Entities.Duels;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class GroupDuelsConfiguration : IEntityTypeConfiguration<GroupDuel>
// {
//     private const string IsConfirmedFieldName = "_isConfirmed";
//     
//     public void Configure(EntityTypeBuilder<GroupDuel> builder)
//     {
//         builder.HasOne(d => d.Group)
//             .WithMany();
//
//         builder.HasOne(d => d.CreatedBy)
//             .WithMany();
//         
//         builder
//             .OwnsOne(d => d.IsConfirmed, b =>
//             {
//                 b.ToJson();
//                 b.UsePropertyAccessMode(PropertyAccessMode.Field);
//                 
//             })
//             .Navigation(d => d.IsConfirmed)
//             .HasField(IsConfirmedFieldName);
//     }
// }
