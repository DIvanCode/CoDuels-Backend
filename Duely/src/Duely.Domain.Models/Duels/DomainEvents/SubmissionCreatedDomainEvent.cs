using Duely.Domain.Kernel.DomainEvents;
using Duely.Domain.Models.Duels.Entities;

namespace Duely.Domain.Models.Duels.DomainEvents;

public sealed class SubmissionCreatedDomainEvent(Submission submission) : DomainEvent
{
    public Submission Submission { get; init; } = submission;
}
