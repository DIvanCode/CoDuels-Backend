// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Duely.Domain.Models.Duels.Entities;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Users.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
// using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class DuelsConfiguration : IEntityTypeConfiguration<Duel>
// {
//     private const string TableName = "Duels";
//     
//     public void Configure(EntityTypeBuilder<Duel> builder)
//     {
//         builder.ToTable(TableName);
//
//         builder.HasKey(d => d.Id);
//         builder.Property(d => d.Id)
//             .ValueGeneratedNever();
//
//         builder.Property(d => d.Type)
//             .HasConversion<string>();
//         builder.HasDiscriminator(d => d.Type)
//             .HasValue<RankedDuel>(DuelType.RankedDuel)
//             .HasValue<FriendlyDuel>(DuelType.FriendlyDuel)
//             .HasValue<GroupDuel>(DuelType.GroupDuel)
//             .HasValue<TournamentDuel>(DuelType.TournamentDuel);
//
//         builder.HasOne(d => d.Configuration)
//             .WithMany()
//             .HasPrincipalKey(c => c.Id);
//
//         builder.HasMany(d => d.Participants)
//             .WithMany();
//
//         builder.Property(d => d.ProblemSet)
//             .HasConversion<ProblemSetConverter>();
//
//         builder.Property(d => d.Status)
//             .HasConversion<string>();
//
//         builder.Property(d => d.CreatedAt);
//         
//         builder.Property(d => d.UpdatedAt)
//             .IsConcurrencyToken();
//         
//         builder.Property(d => d.StartedAt);
//         
//         builder.Property(d => d.FinishedAt);
//
//         builder.HasOne(d => d.Winner)
//             .WithMany();
//     }
// }
//
// internal sealed class ProblemSetConverter : ValueConverter<ProblemSet, string>
// {
//     private static readonly JsonSerializerOptions Options = new();
//     
//     public ProblemSetConverter()
//         : base(
//             from => JsonSerializer.Serialize(from, Options),
//             to => JsonSerializer.Deserialize<ProblemSet>(to, Options)!)
//     {
//     }
// }
