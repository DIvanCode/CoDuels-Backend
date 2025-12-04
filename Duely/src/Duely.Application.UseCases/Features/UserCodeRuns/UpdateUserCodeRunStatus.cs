using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Errors;
using Duely.Domain.Models;


namespace Duely.Application.UseCases.Features.UserCodeRuns;

public sealed class UpdateUserCodeRunStatusCommand : IRequest<Result>
{
    public required string ExecutionId { get; init; }
    public required string Type { get; init; }
    public string? StepName { get; init; }
    public string? Status { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateUserCodeRunStatusHandler(Context context) : IRequestHandler<UpdateUserCodeRunStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCodeRunStatusCommand command, CancellationToken cancellationToken)
    {
        var run = await context.UserCodeRuns.SingleOrDefaultAsync(r => r.ExecutionId == command.ExecutionId, cancellationToken);

        if (run is null)
        {
            return new EntityNotFoundError(nameof(UserCodeRun), nameof(UserCodeRun.ExecutionId), command.ExecutionId);
        }

        if (run.Status == UserCodeRunStatus.Done)
        {
            return Result.Ok();
        }

        if (command.Type is "start" or "compile" or "run")
        {
            run.Status = UserCodeRunStatus.Running;
        }

        if (!string.IsNullOrEmpty(command.Error))
        {
            run.Status = UserCodeRunStatus.Done;
            run.Error = command.Error;
        }

        if (command.Type == "run" && !string.IsNullOrEmpty(command.Status))
        {
            if (command.Status == "OK")
            {
                run.Status = UserCodeRunStatus.Done;
                run.Output = command.Output ?? "";
                run.Error = null;
            }
            else
            {
                run.Status = UserCodeRunStatus.Done;
                run.Error = command.Error ?? command.Status;
            }
        }

        if (command.Type == "finish" && string.IsNullOrEmpty(command.Error) 
            && run.Status != UserCodeRunStatus.Done)
        {
            run.Status = UserCodeRunStatus.Done;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}