using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(s => s.UserId)
            .HasColumnName("UserId")
            .HasColumnType("integer");

        builder.Property(s => s.Code)
            .HasColumnName("Code")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.Language)
            .HasColumnName("Language")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.SubmitTime)
            .HasColumnName("SubmitTime")
            .HasColumnType("timestamp");

        builder.Property(s => s.Status)
            .HasColumnName("Status")
            .HasConversion<string>()
            .HasColumnType("text");

        builder.Property(s => s.Verdict)
            .HasColumnName("Verdict")
            .HasColumnType("text")
            .IsRequired(false);
        
        builder.Property<int>("DuelId")
            .HasColumnName("DuelId")
            .HasColumnType("integer")
            .IsRequired();

        builder.HasOne(s => s.Duel)
            .WithMany()
            .HasForeignKey("DuelId");
    }
}
