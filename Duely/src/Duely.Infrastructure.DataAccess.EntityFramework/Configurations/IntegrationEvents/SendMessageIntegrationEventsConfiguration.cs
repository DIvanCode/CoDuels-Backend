using System.Text.Json;
using System.Text.Json.Serialization;
using Duely.Domain.Models.Users.Entities;
using Duely.Infrastructure.Gateway.Client.Abstracts;
using Duely.Infrastructure.IntegrationEvents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations.IntegrationEvents;

internal sealed class SendMessageIntegrationEventsConfiguration
    : IEntityTypeConfiguration<SendMessageIntegrationEvent>
{
    private static readonly JsonSerializerOptions MessageSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public void Configure(EntityTypeBuilder<SendMessageIntegrationEvent> builder)
    {
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId);
        
        builder.Property(e => e.Message)
            .HasConversion(
                from => JsonSerializer.Serialize(from, MessageSerializerOptions),
                to => JsonSerializer.Deserialize<Message>(to, MessageSerializerOptions)!);
            
        builder.Property(e => e.ExpirationTime);
    }
}
