using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.IntegrationEvents.Models;
using Duely.Infrastructure.Problems.Abstracts;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace Duely.Infrastructure.IntegrationEvents.Handlers;

internal sealed class ProcessSubmissionIntegrationEventHandler(
    Context context,
    IProblemsGateway problemsGateway)
    : IntegrationEventHandler<ProcessSubmissionIntegrationEvent>
{
    public override IntegrationEventType SupportedType => IntegrationEventType.ProcessSubmission;
    
    public override async Task<Result> Handle(
        ProcessSubmissionIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var submission = await context.Submissions
            .Where(s => s.Id == integrationEvent.SubmissionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (submission is null)
        {
            return Result.Ok();
        }
        
        
    }
}
