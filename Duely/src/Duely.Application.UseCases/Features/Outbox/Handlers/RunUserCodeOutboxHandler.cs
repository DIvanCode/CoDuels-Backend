using Duely.Application.UseCases.Payloads;
using Duely.Application.UseCases.Features.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using Duely.Application.UseCases.Features.UserCodeRuns;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Duely.Infrastructure.DataAccess.EntityFramework;



namespace Duely.Application.UseCases.Features.Outbox.Handlers;

public sealed class RunUserCodeOutboxHandler (
    IExeshClient client,
    Context context
) : IOutboxHandler<RunUserCodePayload>
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
        run.Status = UserCodeRunStatus.Running;
        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Ok();
    }
}