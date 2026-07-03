using Duely.Infrastructure.IntegrationEvents.Models;
using FluentResults;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class ProcessSubmissionIntegrationEventHandler()
    : IntegrationEventHandler<ProcessSubmissionIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.ProcessSubmission;
    
    public override async Task<Result> Handle(
        ProcessSubmissionIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        return new IntegrationEventExpiredError();
    }
}
