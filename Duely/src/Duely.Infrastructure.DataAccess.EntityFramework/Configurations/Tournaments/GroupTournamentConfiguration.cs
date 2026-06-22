// using Duely.Domain.Models.Tournaments.Entities.Tournaments;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments;
//
// internal sealed class GroupTournamentConfiguration : IEntityTypeConfiguration<GroupTournament>
// {
//     
//     public void Configure(EntityTypeBuilder<GroupTournament> builder)
//     {
//         builder.HasOne(t => t.Group)
//             .WithMany();
//         
//         // builder.HasIndex(t => t.Group);
//     }
// }
