using Duely.Application.Services.Errors;
using Duely.Domain.Models;
using Duely.Domain.Models.Messages;
using Duely.Domain.Models.Outbox;
using Duely.Domain.Models.Outbox.Payloads;
using Duely.Infrastructure.DataAccess.EntityFramework;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Duely.Application.UseCases.Features.CodeRuns;

public sealed class UpdateCodeRunCommand : IRequest<Result>
{
    public required string ExecutionId { get; init; }
    public required string Type { get; init; }
    public string? Status { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateCodeRunHandler(Context context) : IRequestHandler<UpdateCodeRunCommand, Result>
{
    public async Task<Result> Handle(UpdateCodeRunCommand command, CancellationToken cancellationToken)
    {
        var codeRun = await context.CodeRuns
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.ExecutionId == command.ExecutionId, cancellationToken);
        if (codeRun is null)
        {
            return new EntityNotFoundError(nameof(CodeRun), nameof(CodeRun.ExecutionId), command.ExecutionId);
        }

        if (codeRun.Status == UserCodeRunStatus.Done)
        {
            return Result.Ok();
        }

        if (command.Type is "start" or "compile" or "run")
        {
            codeRun.Status = UserCodeRunStatus.Running;
        }

        if (!string.IsNullOrEmpty(command.Error))
        {
            codeRun.Status = UserCodeRunStatus.Done;
            codeRun.Error = command.Error;
        }

        if (command.Type == "run" && !string.IsNullOrEmpty(command.Status))
        {
            if (command.Status == "OK")
            {
                codeRun.Status = UserCodeRunStatus.Done;
                codeRun.Output = command.Output ?? "";
                codeRun.Error = null;
            }
            else
            {
                codeRun.Status = UserCodeRunStatus.Done;
                codeRun.Error = command.Error;
            }
        }

        if (command.Type == "finish" &&
            string.IsNullOrEmpty(command.Error) && 
            codeRun.Status != UserCodeRunStatus.Done)
        {
            codeRun.Status = UserCodeRunStatus.Done;
        }

        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.SendMessage,
            Payload = new SendMessagePayload
            {
                UserId = codeRun.User.Id,
                Message = new CodeRunStatusUpdatedMessage
                {
                    RunId = codeRun.Id,
                    Status = codeRun.Status,
                    Error = codeRun.Error
                }
            },
            RetryUntil = DateTime.UtcNow.AddSeconds(10)
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
