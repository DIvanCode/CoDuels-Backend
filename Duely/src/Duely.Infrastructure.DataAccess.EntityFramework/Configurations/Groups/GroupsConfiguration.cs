// using Duely.Domain.Models.Groups.Entities;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
//
// namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.Groups;
//
// internal sealed class GroupsConfiguration : IEntityTypeConfiguration<Group>
// {
//     private const string TableName = "Groups";
//     private const string MembershipsTableName = "GroupMemberships";
//     private const string MembershipsFieldName = "_memberships";
//     
//     public void Configure(EntityTypeBuilder<Group> builder)
//     {
//         builder.ToTable(TableName);
//
//         builder.HasKey(g => g.Id);
//         builder.Property(g => g.Id)
//             .ValueGeneratedNever();
//
//         builder.Property(g => g.Name);
//
//         builder.HasMany(g => g.Memberships, b =>
//             {
//                 b.ToTable(MembershipsTableName);
//                 
//                 b.HasOne(m => m.Group)
//                     .WithMany();
//
//                 b.HasOne(m => m.User)
//                     .WithMany();
//
//                 b.Property(m => m.Role)
//                     .HasConversion<string>();
//
//                 b.Property(m => m.IsConfirmed);
//             })
//             .Navigation(g => g.Memberships)
//             .HasField(MembershipsFieldName)
//             .UsePropertyAccessMode(PropertyAccessMode.Field);
//     }
// }
