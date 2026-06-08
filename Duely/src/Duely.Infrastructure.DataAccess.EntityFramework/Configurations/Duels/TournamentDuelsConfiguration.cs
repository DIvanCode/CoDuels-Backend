// using Duely.Domain.Models.Duels.Entities.Duels;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class TournamentDuelsConfiguration : IEntityTypeConfiguration<TournamentDuel>
// {
//     private const string IsConfirmedFieldName = "_isConfirmed";
//     
//     public void Configure(EntityTypeBuilder<TournamentDuel> builder)
//     {
//         builder.HasOne(d => d.Tournament)
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
