using System.Text.Json.Serialization;

namespace Duely.Application.UseCases.Dtos;

public sealed class TicketDto
{
    [JsonPropertyName("ticket")]
    public required string Ticket { get; init; }
}
