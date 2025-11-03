using Duely.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("Outbox");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(o => o.Type)
            .HasColumnName("Type")
            .HasColumnType("text")
            .HasConversion<string>()       
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnName("Payload")
            .HasColumnType("text")        
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("Status")
            .HasColumnType("text")
            .HasConversion<string>()      
            .IsRequired();

        builder.Property(o => o.Retries)
            .HasColumnName("Retries")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(o => o.RetryAt)
            .HasColumnName("RetryAt")
            .HasColumnType("timestamp")    
            .IsRequired(false);
    }
}
