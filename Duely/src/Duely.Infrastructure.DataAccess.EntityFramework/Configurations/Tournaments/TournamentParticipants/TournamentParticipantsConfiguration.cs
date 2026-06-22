// using Duely.Domain.Models.Tournaments.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Tournaments.TournamentParticipants;
//
// public sealed class TournamentParticipantsConfiguration : IEntityTypeConfiguration<TournamentParticipant>
// {
//     private const string TableName = "TournamentParticipants";
//
//     public void Configure(EntityTypeBuilder<TournamentParticipant> builder)
//     {
//         builder.ToTable(TableName);
//
//         // builder.HasKey(t => new { t.Tournament, t.User });
//
//         builder.HasOne(p => p.Tournament)
//             .WithMany();
//
//         builder.HasOne(p => p.User)
//             .WithMany();
//
//         builder.Property(p => p.Seed);
//     }
// }
