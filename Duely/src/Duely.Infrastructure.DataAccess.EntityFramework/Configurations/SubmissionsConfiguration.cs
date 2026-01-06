using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.HasOne(s => s.User)
            .WithMany()
            .IsRequired();

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
            .HasColumnType("timestamp")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("Status")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.Verdict)
            .HasColumnName("Verdict")
            .HasColumnType("text")
            .IsRequired(false);
        
        builder.Property(s => s.Message)
            .HasColumnName("Message")
            .HasColumnType("text")
            .IsRequired(false);
        
        builder.Property(s => s.IsUpsolve)
            .HasColumnName("IsUpsolve")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasOne(s => s.Duel)
            .WithMany(d => d.Submissions)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
