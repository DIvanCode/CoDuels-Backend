using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class UserCodeRunConfiguration : IEntityTypeConfiguration<UserCodeRun>
{
    public void Configure(EntityTypeBuilder<UserCodeRun> builder)
    {
        builder.ToTable("UserCodeRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.HasOne(r => r.User)
            .WithMany()
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Code)
            .HasColumnName("Code")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Language)
            .HasColumnName("Language")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Input)
            .HasColumnName("Input")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("Status")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Output)
            .HasColumnName("Output")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(r => r.Error)
            .HasColumnName("Error")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(r => r.ExecutionId)
            .HasColumnName("ExecutionId")
            .HasColumnType("text")
            .IsRequired(false);
    }
}
