// using Duely.Domain.Models.Tournaments.Entities.Configurations;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments.TournamentConfigurations;
//
// internal sealed class SingleEliminationBracketTournamentConfigurationsConfiguration
//     : IEntityTypeConfiguration<SingleEliminationBracketTournamentConfiguration>
// {
//     private const string NodesFieldName = "_nodes";
//     
//     public void Configure(EntityTypeBuilder<SingleEliminationBracketTournamentConfiguration> builder)
//     {
//         builder
//             .OwnsMany(c => c.Nodes, b =>
//             {
//                 b.ToJson();
//                 b.UsePropertyAccessMode(PropertyAccessMode.Field);
//                 
//             })
//             .Navigation(c => c.Nodes)
//             .HasField(NodesFieldName);
//     }
// }
