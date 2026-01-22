using System.Text.Json.Serialization;

namespace Duely.Domain.Models.Outbox.Payloads;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SendMessagePayload), nameof(SendMessagePayload))]
[JsonDerivedType(typeof(TestSolutionPayload), nameof(TestSolutionPayload))]
[JsonDerivedType(typeof(RunCodePayload), nameof(RunCodePayload))]
public abstract class OutboxPayload;
