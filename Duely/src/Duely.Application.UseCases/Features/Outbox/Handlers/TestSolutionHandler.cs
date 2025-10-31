using FluentResults;
using Duely.Application.UseCases.Payloads;        
using Duely.Application.UseCases.Features.Outbox.Relay;     
using Duely.Domain.Models;                                  
using Duely.Infrastructure.Gateway.Tasks.Abstracts;         

namespace Duely.Application.UseCases.Features.Outbox.Handlers;

public sealed class TestSolutionHandler(ITaskiClient client): IOutboxHandler<TestSolutionPayload>
{
    public OutboxType Type => OutboxType.TestSolution;

    public async Task<Result> HandleAsync(TestSolutionPayload payload, CancellationToken cancellationToken)
    {
        var result = await client.TestSolutionAsync(
            payload.TaskId,
            payload.SubmissionId.ToString(),
            payload.Code,
            payload.Language,
            cancellationToken);

        return result;
    }
}
