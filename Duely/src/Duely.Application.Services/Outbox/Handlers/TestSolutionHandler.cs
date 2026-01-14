using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Tasks.Abstracts;
using FluentResults;

namespace Duely.Application.Services.Outbox.Handlers;

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
