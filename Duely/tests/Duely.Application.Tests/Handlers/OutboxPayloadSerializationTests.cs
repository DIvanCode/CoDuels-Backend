using System.Text.Json;
using Duely.Domain.Models;
using Duely.Domain.Models.Duels;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox.Payloads;
using FluentAssertions;
using Xunit;

namespace Duely.Application.Tests.Handlers;

public class OutboxPayloadSerializationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_deserialize_preserves_payload_type()
    {
        var payload = new SendMessagePayload
        {
            UserId = 10,
            Message = new OpponentSolutionUpdatedMessage
            {
                DuelId = 99,
                TaskKey = "A",
                Solution = "print(1)",
                Language = Language.Python
            }
        };

        var json = JsonSerializer.Serialize(payload, Json);
        json.Should().Contain("\"type\":\"OpponentSolutionUpdated\"");

        var parsed = JsonSerializer.Deserialize<SendMessagePayload>(json, Json);
        parsed.Should().NotBeNull();
        parsed!.UserId.Should().Be(10);
        parsed.Message.Should().BeOfType<OpponentSolutionUpdatedMessage>();
        var codeMessage = (OpponentSolutionUpdatedMessage)parsed.Message;
        codeMessage.DuelId.Should().Be(99);
        codeMessage.TaskKey.Should().Be("A");
        codeMessage.Solution.Should().Be("print(1)");
        codeMessage.Language.Should().Be(Language.Python);
    }

    [Fact]
    public void Serialize_deserialize_preserves_invitation_canceled_payload_fields()
    {
        var payload = new SendMessagePayload
        {
            UserId = 5,
            Message = new DuelInvitationCanceledMessage
            {
                OpponentNickname = "op"
            }
        };

        var json = JsonSerializer.Serialize(payload, Json);
        json.Should().Contain("\"type\":\"DuelInvitationCanceled\"");

        var parsed = JsonSerializer.Deserialize<SendMessagePayload>(json, Json);
        parsed.Should().NotBeNull();
        parsed!.UserId.Should().Be(5);
        parsed.Message.Should().BeOfType<DuelInvitationCanceledMessage>();
        var cancelMessage = (DuelInvitationCanceledMessage)parsed.Message;
        cancelMessage.OpponentNickname.Should().Be("op");
    }
}

