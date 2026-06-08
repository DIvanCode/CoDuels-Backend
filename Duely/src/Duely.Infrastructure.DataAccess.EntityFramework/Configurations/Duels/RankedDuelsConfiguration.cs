// using System.Collections.ObjectModel;
// using System.Text.Json;
// using Duely.Domain.Models.Duels.Entities.Duels;
// using Duely.Domain.Models.Users.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
// using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Duels;
//
// internal sealed class RankedDuelsConfiguration : IEntityTypeConfiguration<RankedDuel>
// {
//     public void Configure(EntityTypeBuilder<RankedDuel> builder)
//     {
//         builder.Property(d => d.InitRatings)
//             .HasConversion<InitRatingsDictionaryConverter>();
//
//         builder.Property(d => d.FinalRatings)
//             .HasConversion<FinalRatingsDictionaryConverter>();
//     }
// }
//
// internal sealed class InitRatingsDictionaryConverter : ValueConverter<Dictionary<UserId, Rating>, string>
// {
//     private static readonly JsonSerializerOptions Options = new();
//     
//     public InitRatingsDictionaryConverter()
//         : base(
//             from => JsonSerializer.Serialize(from, Options),
//             to => JsonSerializer.Deserialize<Dictionary<UserId,Rating>>(to, Options)!)
//     {
//     }
// }
//
// internal sealed class FinalRatingsDictionaryConverter : ValueConverter<Dictionary<UserId, Rating>?, string>
// {
//     private static readonly JsonSerializerOptions Options = new();
//     
//     public FinalRatingsDictionaryConverter()
//         : base(
//             from => JsonSerializer.Serialize(from, Options),
//             to => JsonSerializer.Deserialize<Dictionary<UserId,Rating>>(to, Options))
//     {
//     }
// }
