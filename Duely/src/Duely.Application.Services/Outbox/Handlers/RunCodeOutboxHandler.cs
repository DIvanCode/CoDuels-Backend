using Duely.Application.Services.Errors;
using Duely.Application.Services.Outbox.Relay;
using Duely.Domain.Models;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Infrastructure.Gateway.Exesh.Abstracts;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.Services.Outbox.Handlers;

public sealed class RunCodeOutboxHandler(IExeshClient client, Context context)
    : IOutboxHandler<RunCodePayload>
{
    public async Task<Result> HandleAsync(RunCodePayload payload, CancellationToken cancellationToken)
    {
        
        var run = await context.CodeRuns
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Id == payload.RunId, cancellationToken);
        if (run is null)
        {
            return new EntityNotFoundError(nameof(CodeRun), nameof(CodeRun.Id), payload.RunId);
        }

        var execResult = await client.ExecuteAsync(
            new ExecuteCodeRequest(
                Code: payload.Code,
                Language: payload.Language.ToString(),
                Input: payload.Input,
                DuelId: run.DuelId,
                UserId: run.User.Id,
                TaskKey: run.TaskKey.ToString(),
                TimeLimit: payload.TimeLimit,
                MemoryLimit: payload.MemoryLimit),
            cancellationToken);

        if (execResult.IsFailed)
        {
            return Result.Fail(execResult.Errors);
        }

        run.ExecutionId = execResult.Value.ExecutionId;

        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Ok();
    }
}
