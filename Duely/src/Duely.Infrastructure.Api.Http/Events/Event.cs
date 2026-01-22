using System.Text.Json.Serialization;

namespace Duely.Infrastructure.Api.Http.Events;

public enum EventType
{
    SolutionUpdated = 0
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SolutionUpdatedEvent), nameof(EventType.SolutionUpdated))]
public abstract class Event;
