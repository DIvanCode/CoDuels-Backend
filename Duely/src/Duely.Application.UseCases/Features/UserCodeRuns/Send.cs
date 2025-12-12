using Duely.Application.UseCases.Dtos;
using Duely.Application.UseCases.Errors;
using Duely.Application.UseCases.Payloads;
using Duely.Domain.Models;
using Duely.Infrastructure.DataAccess.EntityFramework;
using Duely.Application.UseCases.Features.RateLimiting;
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Duely.Application.UseCases.Features.UserCodeRuns;

public sealed class RunUserCodeCommand : IRequest<Result<UserCodeRunDto>>
{
    public required int UserId { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
    public required string Input { get; init; }
}

public sealed class RunUserCodeHandler(Context context, IRunUserCodeLimiter runUserCodeLimiter)
    : IRequestHandler<RunUserCodeCommand, Result<UserCodeRunDto>>
{
    public async Task<Result<UserCodeRunDto>> Handle(RunUserCodeCommand command, CancellationToken cancellationToken)
    {

        if (await runUserCodeLimiter.IsLimitExceededAsync(command.UserId, cancellationToken))
        {
            return new RateLimitExceededError("Too many code runs.");
        }

        var user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return new EntityNotFoundError(nameof(User), nameof(User.Id), command.UserId);
        }
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try 
        {
            var run = new UserCodeRun
            {
                User = user,
                Code = command.Code,
                Language = command.Language,
                Input = command.Input,
                Status = UserCodeRunStatus.Queued,
                Output = null,
                Error = null,
                ExecutionId = null,
                CreatedAt = DateTime.UtcNow
            };

            context.UserCodeRuns.Add(run);
            await context.SaveChangesAsync(cancellationToken);

            var payload = JsonSerializer.Serialize(new RunUserCodePayload(run.Id, run.Code, run.Language, run.Input));
            var retryUntil = DateTime.UtcNow.AddMinutes(5);
            context.Outbox.Add(new OutboxMessage
                {
                    Type = OutboxType.RunUserCode,
                    Status = OutboxStatus.ToDo,
                    Retries = 0,
                    RetryAt = null,
                    Payload = payload,
                    RetryUntil = retryUntil
                });

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

            return new UserCodeRunDto
            {
                RunId = run.Id,
                Code = run.Code,
                Language = run.Language,
                Input = run.Input,
                Status = run.Status,
                Output = run.Output,
                Error = run.Error
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
