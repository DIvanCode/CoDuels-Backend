using Duely.Application.Services.Outbox.Payloads;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Services.Outbox.Handlers;

public sealed class RunUserCodeOutboxHandler(IExeshClient client, Context context)
    : IOutboxHandler<RunUserCodePayload>
{
    public OutboxType Type => OutboxType.RunUserCode;

    public async Task<Result> HandleAsync(RunUserCodePayload payload, CancellationToken cancellationToken)
    {
        
        var run = await context.UserCodeRuns.SingleOrDefaultAsync(r => r.Id == payload.RunId, cancellationToken);

        if (run is null)
        {
            return Result.Fail($"Run {payload.RunId} not found");
        }

        var steps = ExeshStepsBuilder.BuildRunSteps(
            payload.Code,
            payload.Language,
            payload.Input);

        var execResult = await client.ExecuteAsync(steps, cancellationToken);

        if (execResult.IsFailed)
        {
            return Result.Fail(execResult.Errors);
        }

        run.ExecutionId = execResult.Value.ExecutionId;

        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Ok();
    }
}